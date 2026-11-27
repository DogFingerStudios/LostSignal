using UnityEngine;
using UnityEngine.Audio;

namespace KeepDriving
{
    [DisallowMultipleComponent]
    public sealed class CarAudio : MonoBehaviour
    {
        [SerializeField] AudioMixerGroup sfxGroup;
        [SerializeField] AudioClip idleClip;
        [SerializeField] AudioClip engineClip;
        [SerializeField] AudioClip boostClip;
        [SerializeField] AudioClip pavementClip;
        [SerializeField] AudioClip gravelClip;

        [Header("Collision one-shots")]
        [SerializeField] AudioClip lowSpeedImpactClip;
        [SerializeField] AudioClip metalCrunchClip;
        [SerializeField] AudioClip hardImpactClip;

        AudioSource idle, engine, boost, pavement, gravel, impact;
        float nextImpactTime;
        bool wasBoosting;

        internal AudioMixerGroup SfxGroup => sfxGroup;

        void Awake()
        {
            if (!sfxGroup) return;

            idle = CreateSource(idleClip, true, .7f);
            engine = CreateSource(engineClip, true, .7f);
            boost = CreateSource(boostClip, false, .7f);
            pavement = CreateSource(pavementClip, true, 0f);
            gravel = CreateSource(gravelClip, true, .7f);

            AudioClip initialImpact = lowSpeedImpactClip ? lowSpeedImpactClip
                : (metalCrunchClip ? metalCrunchClip : hardImpactClip);
            impact = CreateSource(initialImpact, false, .65f);
            if (impact) impact.volume = 1f;
        }

        AudioSource CreateSource(AudioClip clip, bool loop, float spatialBlend)
        {
            if (!clip) return null;

            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = sfxGroup;
            source.clip = clip;
            source.loop = loop;
            source.volume = 0f;
            source.spatialBlend = spatialBlend;
            source.dopplerLevel = 0f;
            source.minDistance = 5f;
            return source;
        }

        internal void UpdateMix(bool driving, bool inCabin, float speed, float speedRatio, float load,
            bool boosting, bool grounded, bool onPavement)
        {
            if (!isActiveAndEnabled) return;

            float dt = Time.unscaledDeltaTime;
            float running = driving ? Mathf.Max(Mathf.InverseLerp(0f, 7f, speed), load * .6f) : 0f;
            float road = driving && grounded ? Mathf.InverseLerp(1f, 24f, speed) : 0f;
            SetLoop(idle, driving ? .24f * (1f - running) : 0f, 1f + .12f * load, dt);
            SetLoop(engine, running * Mathf.Lerp(.16f, .44f, load),
                Mathf.Lerp(.72f, 1.4f, speedRatio) + .15f * load, dt);

            // The pavement recording is from inside the cabin. Use a quieter bed in chase view.
            SetLoop(pavement, onPavement ? road * (inCabin ? .30f : .17f) : 0f,
                Mathf.Lerp(.75f, 1.15f, road), dt);
            SetLoop(gravel, onPavement ? 0f : road * .29f, Mathf.Lerp(.7f, 1.2f, road), dt);

            if (boost)
            {
                if (!driving) boost.Stop();
                else if (boosting && !wasBoosting)
                {
                    boost.volume = .27f;
                    boost.Play();
                }
            }
            wasBoosting = driving && boosting;
        }

        internal void PlayImpact(float speedIntoSurface)
        {
            const float minimumSpeed = 1.1f;
            if (!isActiveAndEnabled || !impact || speedIntoSurface < minimumSpeed ||
                Time.unscaledTime < nextImpactTime) return;

            AudioClip clip = speedIntoSurface >= 8f ? hardImpactClip
                : (speedIntoSurface >= 3.5f ? metalCrunchClip : lowSpeedImpactClip);
            if (!clip) clip = impact.clip;

            nextImpactTime = Time.unscaledTime + .4f;
            float strength = Mathf.InverseLerp(minimumSpeed, 14f, speedIntoSurface);
            impact.PlayOneShot(clip, Mathf.Lerp(.20f, .85f, strength));
        }

        static void SetLoop(AudioSource source, float volume, float pitch, float dt)
        {
            if (!source) return;

            source.pitch = Mathf.MoveTowards(source.pitch, pitch, 2f * dt);
            source.volume = Mathf.MoveTowards(source.volume, volume, 1.6f * dt);
            if (source.volume > 0f)
            {
                if (!source.isPlaying) source.Play();
            }
            else if (source.isPlaying) source.Stop();
        }

        void OnDisable()
        {
            if (idle) idle.Stop();
            if (engine) engine.Stop();
            if (boost) boost.Stop();
            if (pavement) pavement.Stop();
            if (gravel) gravel.Stop();
            if (impact) impact.Stop();
            nextImpactTime = 0f;
            wasBoosting = false;
        }

        void OnDestroy()
        {
            if (idle) Destroy(idle);
            if (engine) Destroy(engine);
            if (boost) Destroy(boost);
            if (pavement) Destroy(pavement);
            if (gravel) Destroy(gravel);
            if (impact) Destroy(impact);
        }
    }
}
