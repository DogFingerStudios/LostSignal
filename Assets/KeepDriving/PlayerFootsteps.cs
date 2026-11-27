using UnityEngine;
using UnityEngine.Audio;

namespace KeepDriving
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerFootsteps : MonoBehaviour
    {
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioClip[] dirtWalk;
        [SerializeField] private AudioClip[] dirtRun;
        [SerializeField] private AudioClip[] gravelWalk;
        [SerializeField] private AudioClip[] gravelRun;

        private AudioSource source;
        private AudioClip lastClip;
        private float stepProgress = .65f;

        private void Awake()
        {
            if (!sfxGroup) return;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = sfxGroup;
            source.spatialBlend = 0f;
        }

        internal void Advance(Vector3 displacement, bool moving, bool grounded, bool running, TerrainStream terrain)
        {
            if (!source || !isActiveAndEnabled) return;
            displacement.y = 0f;
            float distance = displacement.magnitude;
            if (!moving || !grounded || distance < .001f)
            {
                stepProgress = .65f;
                return;
            }

            // The prototype moves at 5 m/s walking and 8.5 m/s running.
            stepProgress += distance / (running ? 3.25f : 2.45f);
            if (stepProgress < 1f) return;
            stepProgress %= 1f;

            bool onRoute = terrain && terrain.IsOnLandRoute(transform.position);
            AudioClip[] clips = onRoute ? (running ? gravelRun : gravelWalk) : (running ? dirtRun : dirtWalk);
            if (clips == null || clips.Length == 0) return;
            int index = Random.Range(0, clips.Length);
            if (clips.Length > 1 && clips[index] == lastClip) index = (index + 1) % clips.Length;
            lastClip = clips[index];
            if (lastClip) source.PlayOneShot(lastClip, running ? .58f : .44f);
        }

        internal void Stop()
        {
            stepProgress = .65f;
            if (source) source.Stop();
        }

        private void OnDisable() => Stop();

        private void OnDestroy()
        {
            if (source) Destroy(source);
        }
    }
}
