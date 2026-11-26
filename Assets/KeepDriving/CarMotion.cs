using UnityEngine;

namespace KeepDriving
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CarMotion : MonoBehaviour
    {
        [Header("Speed Limits (km/h)")]
        [SerializeField, Min(0f)] private float maxForwardKmh = 140f;
        [SerializeField, Min(0f)] private float maxBoostedForwardKmh = 165f;
        [SerializeField, Min(0f)] private float maxReverseKmh = 25.2f;
        [SerializeField, Min(0f)] private float maxBoostedReverseKmh = 30f;

        [Header("Acceleration and Braking")]
        [SerializeField, Min(0f)] private float acceleration = 8f;
        [SerializeField, Min(0f)] private float boostedAcceleration = 10.5f;
        [SerializeField, Min(0f)] private float topSpeedAcceleration = 2f;
        [SerializeField, Min(0f)] private float handbrakeDeceleration = 6f;
        [SerializeField, Min(0f)] private float directionChangeDeceleration = 11f;
        [SerializeField, Min(0f)] private float directionChangeSpeed = .5f;
        [SerializeField, Min(0f)] private float rollingDrag = .55f;
        [SerializeField, Min(0f)] private float speedSquaredDrag = .0018f;

        [Header("Grip")]
        [SerializeField, Min(0f)] private float lateralGrip = 7f;
        [SerializeField, Min(0f)] private float handbrakeLateralGrip = 1.3f;
        [SerializeField, Min(0f)] private float handbrakeSlideSpeed = 5f;
        [SerializeField, Range(0f, 1f)] private float offRouteTraction = .76f;

        [Header("Steering")]
        [SerializeField, Min(0f)] private float steeringResponse = 5f;
        [SerializeField, Min(0f)] private float lowSpeedYawRate = 82f;
        [SerializeField, Min(0f)] private float highSpeedYawRate = 24f;
        [SerializeField, Min(0f)] private float yawFadeStartSpeed = 6f;
        [SerializeField, Min(0f)] private float yawFadeEndSpeed = 36f;
        [SerializeField, Min(0f)] private float fullSteeringSpeed = 4f;
        [SerializeField, Min(0f)] private float handbrakeYawMultiplier = 1.25f;

        [Header("Ground Contact")]
        [SerializeField, Min(0f)] private float lowSpeedContactLimit = 7f;
        [SerializeField, Min(0f)] private float lowSpeedContactTolerance = .12f;
        [SerializeField, Min(0f)] private float contactTolerance = .003f;

        public float MaxForwardKmh => maxForwardKmh;
        public float MaxBoostedForwardKmh => maxBoostedForwardKmh;
        public float OffRouteTraction => offRouteTraction;

        private const float KmhPerMetrePerSecond = 3.6f;
        private const float ThrottleDeadzone = .01f;

        public float StepSteering(float steering, float input, float dt)
        {
            return Mathf.MoveTowards(steering, input, steeringResponse * dt);
        }

        // Velocity is horizontal. The controller applies ground support and gravity separately.
        public Vector3 Step(Vector3 velocity, Vector3 forward, float throttle, bool boost,
            bool handbrake, float dt, float traction = 1f)
        {
            forward.y = 0f;
            forward.Normalize();
            velocity.y = 0f;
            float longitudinal = Vector3.Dot(velocity, forward);
            Vector3 sideways = velocity - forward * longitudinal;

            if (handbrake)
                longitudinal = Mathf.MoveTowards(longitudinal, 0f, handbrakeDeceleration * traction * dt);
            else if (Mathf.Abs(throttle) > ThrottleDeadzone)
            {
                bool reversingDirection = (throttle < 0f && longitudinal > directionChangeSpeed)
                    || (throttle > 0f && longitudinal < -directionChangeSpeed);
                if (reversingDirection)
                    longitudinal = Mathf.MoveTowards(longitudinal, 0f, directionChangeDeceleration * traction * dt);
                else
                {
                    float limit = throttle > 0f
                        ? (boost ? maxBoostedForwardKmh : maxForwardKmh)
                        : (boost ? maxBoostedReverseKmh : maxReverseKmh);
                    float target = throttle * limit / KmhPerMetrePerSecond;
                    float speedRatio = Mathf.InverseLerp(0f, Mathf.Abs(target), Mathf.Abs(longitudinal));
                    float driveAcceleration = Mathf.Lerp(boost ? boostedAcceleration : acceleration, topSpeedAcceleration, speedRatio);
                    longitudinal = Mathf.MoveTowards(longitudinal, target, driveAcceleration * traction * dt);
                }
            }

            float grip = handbrake && velocity.sqrMagnitude > handbrakeSlideSpeed * handbrakeSlideSpeed
                ? handbrakeLateralGrip : lateralGrip;
            sideways *= Mathf.Exp(-grip * traction * dt);
            Vector3 result = forward * longitudinal + sideways;
            if (!handbrake && Mathf.Abs(throttle) > ThrottleDeadzone) return result;

            float drag = rollingDrag + speedSquaredDrag * velocity.sqrMagnitude;
            return Vector3.MoveTowards(result, Vector3.zero, drag * dt);
        }

        public float YawRate(float steer, float longitudinalSpeed, float traction, bool handbrake)
        {
            float speed = Mathf.Abs(longitudinalSpeed);
            float speedRatio = Mathf.InverseLerp(yawFadeStartSpeed, yawFadeEndSpeed, speed);
            float authority = Mathf.Lerp(lowSpeedYawRate, highSpeedYawRate, Mathf.SmoothStep(0f, 1f, speedRatio));
            if (handbrake && speed > handbrakeSlideSpeed) authority *= handbrakeYawMultiplier;
            return steer * authority * traction * Mathf.Clamp(longitudinalSpeed / Mathf.Max(fullSteeringSpeed, Mathf.Epsilon), -1f, 1f);
        }

        public bool HasGroundContact(float supportHeight, float ballisticHeight, bool wasGrounded, float speed)
        {
            float tolerance = wasGrounded && speed < lowSpeedContactLimit ? lowSpeedContactTolerance : contactTolerance;
            return supportHeight >= ballisticHeight - tolerance;
        }
    }
}
