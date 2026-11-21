using UnityEngine;

namespace KeepDriving
{
    // Ground-plane momentum in metres/second. Ground adhesion is handled separately.
    public static class CarMotion
    {
        public static Vector3 Step(Vector3 velocity, Vector3 forward, float throttle, bool boost, bool brake, float dt)
        {
            forward.y = 0; forward.Normalize();
            velocity.y = 0;
            float longitudinal = Vector3.Dot(velocity, forward);
            Vector3 sideways = velocity - forward * longitudinal;
            float cap = throttle >= 0 ? (boost ? 50 : 32f) : (boost ? 11f : 7f);

            if (!brake && Mathf.Abs(throttle) > .01f)
                longitudinal = Mathf.MoveTowards(longitudinal, throttle * cap, (boost ? 10f : 7f) * dt);
            // Locked tyres retain lateral momentum, so turning under heavy braking produces a slide.
            float grip = brake && velocity.magnitude > 5f ? 1.1f : 9f;
            sideways *= Mathf.Exp(-grip * dt);
            float resistance = brake ? 8f : .55f + .006f * velocity.sqrMagnitude;
            Vector3 result = forward * longitudinal + sideways;
            return Vector3.MoveTowards(result, Vector3.zero, resistance * dt);
        }
    }
}
