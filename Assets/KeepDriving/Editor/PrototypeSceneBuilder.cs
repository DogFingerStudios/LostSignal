using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KeepDriving.Editor
{
    [InitializeOnLoad]
    public static class PrototypeSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/KeepDriving.unity";
        static PrototypeSceneBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (!Application.isBatchMode && !EditorApplication.isPlayingOrWillChangePlaymode && !System.IO.File.Exists(ScenePath)) Build();
            };
        }
        [MenuItem("Keep Driving/Create Prototype Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var ground = new GameObject("Terrain (Paintable Rolling Hills)");
            var terrainStream = PrototypeTerrain.Configure(ground);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 2;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientLight = new Color(.55f, .6f, .68f);
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player Capsule (1.8m)";
            player.transform.position = new Vector3(0, 1, -5);
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = .35f; controller.stepOffset = .3f;
            player.GetComponent<Renderer>().sharedMaterial = Material("Player", new Color(.8f, .65f, .25f));
            var camera = new GameObject("Player Camera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.nearClipPlane = .05f; camera.farClipPlane = 700;
            camera.transform.position = player.transform.position + Vector3.up * .65f;
            camera.gameObject.AddComponent<AudioListener>();
            var game = new GameObject("Prototype Controller").AddComponent<PrototypeController>();
            game.terrainStream = terrainStream;
            game.player = controller; game.playerVisual = player.GetComponent<Renderer>(); game.view = camera;
            var settings = new SerializedObject(game);
            settings.FindProperty("workTruckPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/KeepDriving/WorkTruck.prefab").GetComponent<Rigidbody>();
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("KEEP_DRIVING_SCENE_READY");
        }
        static Material Material(string name, Color color)
        {
            string path = "Assets/KeepDriving/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.color = color;
            return material;
        }
    }
}
