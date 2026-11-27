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
            PrototypeValidation.Validate();
            var game = UnityEngine.Object.FindFirstObjectByType<PrototypeController>();
            var motion = game.car.GetComponent<CarMotion>();
            Check(motion, "Car has a CarMotion component");
            Vector3 normal = Vector3.zero, boosted = Vector3.zero;
            for (int i = 0; i < 1800; i++)
            {
                normal = motion.Step(normal, Vector3.forward, 1, false, false, .02f);
                boosted = motion.Step(boosted, Vector3.forward, 1, true, false, .02f);
            }
            Check(Mathf.Abs(normal.magnitude * 3.6f - motion.MaxForwardKmh) < .1f, "Normal reaches configured top speed");
            Check(Mathf.Abs(boosted.magnitude * 3.6f - motion.MaxBoostedForwardKmh) < .1f, "Shift reaches configured boost speed");
            Vector3 reversing = motion.Step(Vector3.forward * 15f, Vector3.forward, -1f, false, false, .1f);
            Check(reversing.z > 0f && reversing.z < 15f, "Reverse input brakes before shifting direction");
            Check(motion.YawRate(1f, 8f, 1f, false) > motion.YawRate(1f, 35f, 1f, false),
                "Steering sensitivity falls at high speed");
            Check(motion.HasGroundContact(1f, .998f, true, 25f), "Truck stays supported at speed on level ground");
            Check(!motion.HasGroundContact(1f, 1.08f, true, 25f), "Uphill momentum carries the truck beyond a crest");
            Check(!motion.HasGroundContact(1f, 1.08f, false, 25f), "Airborne truck stays free until touchdown");
            Check(motion.HasGroundContact(1f, .99f, false, 25f), "Truck regains contact when descending to terrain");
            Vector3 coast = boosted, brake = boosted;
            for (int i = 0; i < 50; i++)
            {
                coast = motion.Step(coast, Vector3.forward, 0, false, false, .02f);
                brake = motion.Step(brake, Vector3.forward, 1, true, true, .02f);
            }
            Check(coast.magnitude > brake.magnitude + 2, "Brake overrides throttle and boost");
            Check(coast.magnitude > 10, "Momentum persists when throttle released");
            Vector3 angled = Quaternion.Euler(0, 25, 0) * Vector3.forward;
            Vector3 grip = motion.Step(boosted, angled, 0, false, false, .1f);
            Vector3 skid = motion.Step(boosted, angled, 0, false, true, .1f);
            float lateralGrip = Vector3.ProjectOnPlane(grip, angled).magnitude;
            float lateralSkid = Vector3.ProjectOnPlane(skid, angled).magnitude;
            Check(lateralSkid > lateralGrip * 1.5f, "Locked tyres allow lateral skid");
            for (int i = 0; i < 500; i++) brake = motion.Step(brake, angled, 1, true, true, .02f);
            Check(brake == Vector3.zero, "Holding brake reaches a stable stop");
            Call(game, "Enter");
            var driver = game.car.transform.Find("Rectangle Body/Seated Bubba");
            Check(driver && driver.gameObject.activeInHierarchy, "Seated Bubba is mounted to the chassis");
            foreach (var collider in driver.GetComponentsInChildren<Collider>(true))
                Check(!collider.enabled, "Seated visual has no interfering collider");
            var thigh = driver.Find("Armature/Skeleton/Handyman/Handyman Pelvis/Handyman L Thigh");
            var knee = thigh ? thigh.Find("Handyman L Calf") : null;
            Check(knee && Vector3.Dot((knee.position - thigh.position).normalized, game.car.transform.forward) > .7f,
                "Driver's knee points forward in the seated pose");
            var third = typeof(PrototypeController).GetField("thirdPerson", BindingFlags.NonPublic | BindingFlags.Instance);
            var headRenderers = (Renderer[])typeof(PrototypeController)
                .GetField("seatedHeadRenderers", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);
            Check(headRenderers.Length > 0, "Driver head meshes found");
            third.SetValue(game, true); Call(game, "UpdateVisibility");
            foreach (var renderer in headRenderers)
                Check(!renderer.forceRenderingOff, "Driver head visible in third person");
            third.SetValue(game, false); Call(game, "UpdateVisibility");
            foreach (var renderer in headRenderers)
                Check(renderer.forceRenderingOff, "Driver head hidden in first person");
            Call(game, "TryExit");
            Check(!driver.gameObject.activeSelf, "Driver disappears on exit");
            SlopeValidation.Validate();
            Debug.Log($"DRIVING_VALIDATION_PASSED: normal={normal.magnitude:F1}m/s boost={boosted.magnitude:F1}m/s, inertia, friction, brake priority, lateral skid, full stop, seated driver, slopes");
        }
        static void Call(PrototypeController game, string name) => typeof(PrototypeController).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, null);
        static void Check(bool result, string label) { if (!result) throw new Exception("Driving validation failed: " + label); }
    }
}
