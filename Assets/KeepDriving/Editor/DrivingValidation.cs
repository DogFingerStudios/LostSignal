using System;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KeepDriving.Editor
{
    public static class DrivingValidation
    {
        public static void Validate()
        {
            Vector3 normal = Vector3.zero, boosted = Vector3.zero;
            for (int i = 0; i < 1800; i++)
            {
                normal = CarMotion.Step(normal, Vector3.forward, 1, false, false, .02f);
                boosted = CarMotion.Step(boosted, Vector3.forward, 1, true, false, .02f);
            }
            Check(Mathf.Abs(normal.magnitude * 3.6f - CarMotion.MaxForwardKmh) < .1f, "Normal reaches configured top speed");
            Check(Mathf.Abs(boosted.magnitude * 3.6f - CarMotion.MaxBoostedForwardKmh) < .1f, "Shift reaches configured boost speed");
            Vector3 reversing = CarMotion.Step(Vector3.forward * 15f, Vector3.forward, -1f, false, false, .1f);
            Check(reversing.z > 0f && reversing.z < 15f, "Reverse input brakes before shifting direction");
            Check(CarMotion.YawRate(1f, 8f, 1f, false) > CarMotion.YawRate(1f, 35f, 1f, false),
                "Steering sensitivity falls at high speed");
            Check(CarMotion.HasGroundContact(1f, .998f, true, 25f), "Truck stays supported at speed on level ground");
            Check(!CarMotion.HasGroundContact(1f, 1.08f, true, 25f), "Uphill momentum carries the truck beyond a crest");
            Check(!CarMotion.HasGroundContact(1f, 1.08f, false, 25f), "Airborne truck stays free until touchdown");
            Check(CarMotion.HasGroundContact(1f, .99f, false, 25f), "Truck regains contact when descending to terrain");
            Vector3 coast = boosted, brake = boosted;
            for (int i = 0; i < 50; i++)
            {
                coast = CarMotion.Step(coast, Vector3.forward, 0, false, false, .02f);
                brake = CarMotion.Step(brake, Vector3.forward, 1, true, true, .02f);
            }
            Check(coast.magnitude > brake.magnitude + 2, "Brake overrides throttle and boost");
            Check(coast.magnitude > 10, "Momentum persists when throttle released");
            Vector3 angled = Quaternion.Euler(0, 25, 0) * Vector3.forward;
            Vector3 grip = CarMotion.Step(boosted, angled, 0, false, false, .1f);
            Vector3 skid = CarMotion.Step(boosted, angled, 0, false, true, .1f);
            float lateralGrip = Vector3.ProjectOnPlane(grip, angled).magnitude;
            float lateralSkid = Vector3.ProjectOnPlane(skid, angled).magnitude;
            Check(lateralSkid > lateralGrip * 1.5f, "Locked tyres allow lateral skid");
            for (int i = 0; i < 500; i++) brake = CarMotion.Step(brake, angled, 1, true, true, .02f);
            Check(brake == Vector3.zero, "Holding brake reaches a stable stop");
            PrototypeValidation.Validate();
            var game = UnityEngine.Object.FindFirstObjectByType<PrototypeController>();
            Call(game, "Enter");
            var driver = game.car.transform.Find("Seated Driver Capsule");
            Check(driver && driver.GetComponent<Collider>() == null, "Seated visual has no interfering collider");
            Check(driver.GetComponent<Renderer>().bounds.max.y > game.car.GetComponent<Collider>().bounds.max.y, "Driver upper capsule above car body");
            var third = typeof(PrototypeController).GetField("thirdPerson", BindingFlags.NonPublic | BindingFlags.Instance);
            third.SetValue(game, true); Call(game, "UpdateVisibility");
            Check(driver.GetComponent<Renderer>().enabled, "Driver visible in third person");
            third.SetValue(game, false); Call(game, "UpdateVisibility");
            Check(game.car.transform.Find("First Person Torso").GetComponent<Renderer>().enabled, "First-person torso visible");
            Call(game, "TryExit");
            Check(!driver.GetComponent<Renderer>().enabled, "Driver disappears on exit");
            SlopeValidation.Validate();
            Debug.Log($"DRIVING_VALIDATION_PASSED: normal={normal.magnitude:F1}m/s boost={boosted.magnitude:F1}m/s, inertia, friction, brake priority, lateral skid, full stop, seated driver, slopes");
        }
        static void Call(PrototypeController game, string name) => typeof(PrototypeController).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, null);
        static void Check(bool result, string label) { if (!result) throw new Exception("Driving validation failed: " + label); }
    }
}
