using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KeepDriving.Editor
{
    public sealed class TreeGeneratorWindow : EditorWindow
    {
        private const string GenerateAction = "Generate Trees";
        private const int MaxTreeCount = 10000;

        [SerializeField] private Terrain _terrain;
        [SerializeField] private int _seed;
        [SerializeField] private int _treeCount = 100;
        [SerializeField] private List<GameObject> _treePrefabs = new List<GameObject>();

        private Vector2 scrollPosition;
        private string result;

        [MenuItem("Keep Driving/Tree Generator")]
        private static void Open()
        {
            var window = GetWindow<TreeGeneratorWindow>("Tree Generator");
            if (!window._terrain)
            {
                window._terrain = Selection.activeGameObject?.GetComponent<Terrain>() ?? Terrain.activeTerrain;
            }
        }

        private void OnGUI()
        {
            _terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", _terrain, typeof(Terrain), true);
            _seed = EditorGUILayout.IntField("Seed", _seed);
            _treeCount = EditorGUILayout.IntSlider("Tree Count", _treeCount, 1, MaxTreeCount);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tree Prefabs", EditorStyles.boldLabel);
            DrawDropArea();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MinHeight(60));
            for (int i = 0; i < _treePrefabs.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _treePrefabs[i] = (GameObject)EditorGUILayout.ObjectField(
                    _treePrefabs[i], typeof(GameObject), false);

                bool remove = GUILayout.Button("Remove", GUILayout.Width(65));
                EditorGUILayout.EndHorizontal();
                if (remove)
                {
                    _treePrefabs.RemoveAt(i--);
                }
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Add Prefab Slot"))
            {
                _treePrefabs.Add(null);
            }

            bool validTerrain = _terrain && _terrain.terrainData && _terrain.gameObject.scene.IsValid()
                && _terrain.gameObject.scene.isLoaded && PrefabStageUtility.GetCurrentPrefabStage() == null;

            var validPrefabs = _treePrefabs.FindAll(IsPrefabAsset);

            if (!validTerrain)
            {
                EditorGUILayout.HelpBox("Select a Terrain in an open scene to generate trees.", MessageType.Info);
            }
            else if (validPrefabs.Count == 0)
            {
                EditorGUILayout.HelpBox("Add at least one prefab from the Project window.", MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(!validTerrain || validPrefabs.Count == 0 || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Generate"))
                {
                    Generate(validPrefabs);
                }
            }

            if (!string.IsNullOrEmpty(result))
            {
                EditorGUILayout.HelpBox(result, MessageType.Info);
            }
        }

        private void DrawDropArea()
        {
            Rect area = GUILayoutUtility.GetRect(0, 42, GUILayout.ExpandWidth(true));
            GUI.Box(area, "Drop tree prefabs here");

            Event current = Event.current;
            if (!area.Contains(current.mousePosition)
                || (current.type != EventType.DragUpdated && current.type != EventType.DragPerform))
                return;

            var droppedPrefabs = new List<GameObject>();
            foreach (UnityEngine.Object item in DragAndDrop.objectReferences)
            {
                if (item is GameObject prefab && IsPrefabAsset(prefab))
                {
                    droppedPrefabs.Add(prefab);
                }
            }

            DragAndDrop.visualMode = droppedPrefabs.Count > 0
                ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

            if (current.type == EventType.DragPerform && droppedPrefabs.Count > 0)
            {
                DragAndDrop.AcceptDrag();
                _treePrefabs.AddRange(droppedPrefabs);
            }
            current.Use();
        }

        private static bool IsPrefabAsset(GameObject candidate)
        {
            return candidate && PrefabUtility.IsPartOfPrefabAsset(candidate)
                && AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(candidate)) == candidate;
        }

        private void Generate(List<GameObject> prefabs)
        {
            TerrainData data = _terrain.terrainData;
            Vector3 origin = _terrain.GetPosition();
            var random = new System.Random(_seed);
            int placed = 0;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(GenerateAction);
            try
            {
                GameObject group = null;
                for (int i = 0; i < _treeCount; i++)
                {
                    float x = (float)random.NextDouble();
                    float z = (float)random.NextDouble();
                    GameObject prefab = prefabs[random.Next(prefabs.Count)];
                    int holeX = Mathf.Min((int)(x * data.holesResolution), data.holesResolution - 1);
                    int holeZ = Mathf.Min((int)(z * data.holesResolution), data.holesResolution - 1);
                    if (data.IsHole(holeX, holeZ)) continue;

                    if (!group)
                    {
                        group = new GameObject($"Generated Trees (Seed {_seed})");
                        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(group, _terrain.gameObject.scene);
                        Undo.RegisterCreatedObjectUndo(group, GenerateAction);
                    }

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
                    instance.transform.position = origin + new Vector3(
                        x * data.size.x, data.GetInterpolatedHeight(x, z), z * data.size.z);

                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                    Undo.RegisterCreatedObjectUndo(instance, GenerateAction);
                    placed++;
                }

                Undo.CollapseUndoOperations(undoGroup);
                if (group)
                {
                    EditorSceneManager.MarkSceneDirty(group.scene);
                    Selection.activeGameObject = group;
                }

                result = placed == _treeCount
                    ? $"Placed {placed} trees."
                    : $"Placed {placed} trees. Skipped {_treeCount - placed} positions over Terrain holes.";
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                result = "Generation failed. See the Console for details.";
                Debug.LogException(exception);
            }
        }
    }
}
