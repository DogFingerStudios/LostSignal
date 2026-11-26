using UnityEngine;

namespace KeepDriving
{
    public static class CarMotion
    {
        public const float MaxForwardKmh = 140f;
        public const float MaxBoostedForwardKmh = 165f;
        public const float MaxReverseKmh = 25.2f;
        public const float MaxBoostedReverseKmh = 30f;

        private const float KmhPerMetrePerSecond = 3.6f;
        private const float ShiftDirectionSpeed = .5f;

        // Velocity is horizontal. The controller applies ground support and gravity separately.
        public static Vector3 Step(Vector3 velocity, Vector3 forward, float throttle, bool boost,
            bool handbrake, float dt, float traction = 1f)
        {
            forward.y = 0f;
            forward.Normalize();
            velocity.y = 0f;
            float longitudinal = Vector3.Dot(velocity, forward);
            Vector3 sideways = velocity - forward * longitudinal;

            if (handbrake)
                longitudinal = Mathf.MoveTowards(longitudinal, 0f, 6f * traction * dt);
            else if (Mathf.Abs(throttle) > .01f)
            {
                bool reversingDirection = (throttle < 0f && longitudinal > ShiftDirectionSpeed)
                    || (throttle > 0f && longitudinal < -ShiftDirectionSpeed);
                if (reversingDirection)
                    longitudinal = Mathf.MoveTowards(longitudinal, 0f, 11f * traction * dt);
                else
                {
                    float limit = throttle > 0f
                        ? (boost ? MaxBoostedForwardKmh : MaxForwardKmh)
                        : (boost ? MaxBoostedReverseKmh : MaxReverseKmh);
                    float target = throttle * limit / KmhPerMetrePerSecond;
                    float speedRatio = Mathf.InverseLerp(0f, Mathf.Abs(target), Mathf.Abs(longitudinal));
                    float acceleration = Mathf.Lerp(boost ? 10.5f : 8f, 2f, speedRatio);
                    longitudinal = Mathf.MoveTowards(longitudinal, target, acceleration * traction * dt);
                }
            }

            float lateralGrip = handbrake && velocity.sqrMagnitude > 25f ? 1.3f : 7f;
            sideways *= Mathf.Exp(-lateralGrip * traction * dt);
            Vector3 result = forward * longitudinal + sideways;
            if (!handbrake && Mathf.Abs(throttle) > .01f) return result;

            float drag = .55f + .0018f * velocity.sqrMagnitude;
            return Vector3.MoveTowards(result, Vector3.zero, drag * dt);
        }

        public static float YawRate(float steer, float longitudinalSpeed, float traction, bool handbrake)
        {
            float speed = Mathf.Abs(longitudinalSpeed);
            float authority = Mathf.Lerp(82f, 24f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(6f, 36f, speed)));
            if (handbrake && speed > 5f) authority *= 1.25f;
            return steer * authority * traction * Mathf.Clamp(longitudinalSpeed / 4f, -1f, 1f);
        }

        public static bool HasGroundContact(float supportHeight, float ballisticHeight, bool wasGrounded, float speed)
        {
            float tolerance = wasGrounded && speed < 7f ? .12f : .003f;
            return supportHeight >= ballisticHeight - tolerance;
        }
    }
}
