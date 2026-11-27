using System;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KeepDriving.Editor
{
    public static class SlopeValidation
    {
        public static void Validate()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/KeepDriving.unity");
            var game = UnityEngine.Object.FindFirstObjectByType<PrototypeController>();
            var previousMode = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                game.player.enabled = false;
                PrototypeValidation.SpawnTestTruck(game);
                game.car.rotation = Quaternion.identity;
                Physics.SyncTransforms();
                var speed = typeof(PrototypeController).GetField("carVelocity", BindingFlags.NonPublic | BindingFlags.Instance);
                var step = typeof(PrototypeController).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
                float peak = 0, tilt = 0, maxStep = 0;
                for (int i = 0; i < 350; i++)
                {
                    Vector3 before = game.car.position;
                    speed.SetValue(game, Vector3.forward * 12f);
                    step.Invoke(game, null);
                    Physics.Simulate(Time.fixedDeltaTime);
                    Physics.SyncTransforms();
                    peak = Mathf.Max(peak, game.car.position.y);
                    tilt = Mathf.Max(tilt, Vector3.Angle(game.car.rotation * Vector3.up, Vector3.up));
                    maxStep = Mathf.Max(maxStep, Mathf.Abs(game.car.position.y - before.y));
                    if (float.IsNaN(game.car.position.y)) throw new Exception("Invalid car position");
                }
                if (peak < 6 || tilt < 5 || maxStep > .2f || game.car.position.z < 70 || game.car.position.y > peak - 2)
                    throw new Exception($"Slope check failed: peak={peak}, tilt={tilt}, maxStep={maxStep}, final={game.car.position}");
                Debug.Log($"SLOPE_VALIDATION_PASSED: climbed and descended; peak={peak:F2}m, tilt={tilt:F1}deg, max vertical step={maxStep:F3}m, end={game.car.position}");
            }
            finally { Physics.simulationMode = previousMode; }
        }
    }
}

