using UnityEngine;

namespace KeepDriving
{
    // Ground-plane momentum in metres/second. Ground adhesion is handled separately.
    public static class CarMotion
    {
        // Change these speed limits in km/h. Shift uses the boosted limits.
        public const float MaxForwardKmh = 1000.2f;
        public const float MaxBoostedForwardKmh = 360f;
        public const float MaxReverseKmh = 25.2f;
        public const float MaxBoostedReverseKmh = 39.6f;

        private const float KmhPerMetrePerSecond = 3.6f;

        public static Vector3 Step(Vector3 velocity, Vector3 forward, float throttle, bool boost, bool brake, float dt)
        {
            forward.y = 0; forward.Normalize();
            velocity.y = 0;
            float longitudinal = Vector3.Dot(velocity, forward);
            Vector3 sideways = velocity - forward * longitudinal;
            float limitKmh = throttle >= 0
                ? (boost ? MaxBoostedForwardKmh : MaxForwardKmh)
                : (boost ? MaxBoostedReverseKmh : MaxReverseKmh);
            float targetSpeed = throttle * limitKmh / KmhPerMetrePerSecond;
            bool powered = !brake && Mathf.Abs(throttle) > .01f;

            if (powered)
            {
                longitudinal = Mathf.MoveTowards(longitudinal, targetSpeed, (boost ? 10f : 7f) * dt);
            }

            // Locked tyres retain lateral momentum, so turning under heavy braking produces a slide.
            float grip = brake && velocity.magnitude > 5f ? 1.1f : 9f;
            sideways *= Mathf.Exp(-grip * dt);

            Vector3 result = forward * longitudinal + sideways;
            // Powered acceleration is net of drag, so the configured speed is reachable.
            if (powered) return result;

            float drag = .55f + .006f * velocity.sqrMagnitude;
            return Vector3.MoveTowards(result, Vector3.zero, (drag + (brake ? 8f : 0f)) * dt);
        }
    }
}
