using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KeepDriving.Editor
{
    public static class PrototypeValidation
    {
        public static void Validate()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/KeepDriving.unity");
            var game = UnityEngine.Object.FindFirstObjectByType<PrototypeController>();
            Check(game && game.player && game.view, "Scene references");
            Check(!game.car && !(bool)Call(game, "NearCar"), "Walking scene has no pre-spawned truck");
            SpawnTestTruck(game);
            Check(game.car && game.horn, "Prefab vehicle references");
            Check(game.horn.clip && game.horn.clip.samples > 0 && game.horn.loop, "Persistent horn audio");
            Check(game.player.height == 1.8f && game.car.GetComponent<BoxCollider>().size.z == 4.4f, "Player/car scale");
            game.player.enabled = false;
            game.player.transform.position = new Vector3(-2, 1, 0);
            game.player.enabled = true;
            Physics.SyncTransforms();
            Check((bool)Call(game, "NearCar"), "Car interaction range");
            Call(game, "Enter");
            Check(!game.player.enabled && !game.playerVisual.enabled, "Entering hides and disables player");
            Call(game, "LateUpdate");
            Check(Vector3.Distance(game.view.transform.position, game.car.position) < 2, "Driving first-person camera");
            Call(game, "TryExit");
            Check(game.player.enabled, "Safe car exit");
            Check(Vector3.Distance(game.player.transform.position, game.car.position) > 1.5f, "Exit clears car body");
            Check(EditorBuildSettings.scenes.Length == 1 && EditorBuildSettings.scenes[0].path.EndsWith("KeepDriving.unity"), "Startup scene");
            Debug.Log("KEEP_DRIVING_VALIDATION_PASSED: references, horn, scale, interaction, enter, camera, exit, startup scene");
        }
        internal static void SpawnTestTruck(PrototypeController game)
        {
            var spawn = typeof(PrototypeController).GetMethod("SpawnWorkTruck", BindingFlags.NonPublic | BindingFlags.Instance);
            var truck = spawn.Invoke(game, new object[] { new Vector3(0f, .75f, 0f), Quaternion.identity });
            var select = typeof(PrototypeController).GetMethod("SelectWorkTruck", BindingFlags.NonPublic | BindingFlags.Instance);
            select.Invoke(game, new[] { truck });
            game.car.gameObject.hideFlags = HideFlags.DontSaveInEditor;
        }

        static object Call(PrototypeController game, string method) => typeof(PrototypeController).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, null);
        static void Check(bool condition, string label) { if (!condition) throw new Exception("Prototype validation failed: " + label); }
    }
}
