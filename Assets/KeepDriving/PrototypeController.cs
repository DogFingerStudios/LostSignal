using UnityEngine;
using UnityEngine.InputSystem;

namespace KeepDriving
{
    public sealed class PrototypeController : MonoBehaviour
    {
        public TerrainStream terrainStream;

        public CharacterController player;
        public Renderer playerVisual;
        public Rigidbody car;
        public Camera view;
        public AudioSource horn;
        [SerializeField] AudioClip tyreSkidClip;
        AudioSource tyreSkid;
        AudioClip generatedTyreSkid;
        float tyreSlip;
        bool driving, thirdPerson;
        float yaw, pitch, verticalSpeed;
        float carVerticalSpeed, steering, bodyPitch, bodyRoll;
        float lastDrivingLook;
        bool carGrounded = true;
        Transform carBody;
        CarMotion carMotion;
        CarAudio carAudio;
        float engineLoad;
        bool boosting, onPavement;
        float lastEscape = -10f, quitHintUntil;
        const float WalkSpeed = 5f;
        readonly Collider[] exitHits = new Collider[16];
        readonly RaycastHit[] groundHits = new RaycastHit[32];
        int carGroundMask = ~0;
        int treeTrunkMask;
        BoxCollider carBox;
        const float TreeContactGap = .02f;
        readonly RaycastHit[] cameraHits = new RaycastHit[32];
        Vector3 carVelocity;
        Renderer seatedDriver, seatedTorso;
        TrailRenderer[] skidMarks;
        Material skidMaterial;
        FieldReceiverUI fieldReceiver;
        TerminalScreen terminalScreen;
        Terminal activeTerminal;
        Minimap minimap;
        WorldMap worldMap;
        bool skidding;
        bool ShiftHeld => Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);

        void Start()
        {
            if (terrainStream)
            {
                worldMap = terrainStream.GetComponent<WorldMap>();
                minimap = terrainStream.GetComponent<Minimap>();
            }
            InitializeCarMotion();
            treeTrunkMask = LayerMask.GetMask("Tree Trunks");
            carGroundMask = ~treeTrunkMask;
            carBox = car.GetComponent<BoxCollider>();
            car.isKinematic = true;
            carBody = car.transform.Find("Rectangle Body");
            carAudio = car.GetComponent<CarAudio>();
            tyreSkid = car.gameObject.AddComponent<AudioSource>();
            if (carAudio && carAudio.SfxGroup)
            {
                horn.outputAudioMixerGroup = carAudio.SfxGroup;
                tyreSkid.outputAudioMixerGroup = carAudio.SfxGroup;
            }
            tyreSkid.playOnAwake = false;
            tyreSkid.loop = true;
            if (!tyreSkidClip) generatedTyreSkid = CreateTyreSkidClip();
            tyreSkid.clip = tyreSkidClip ? tyreSkidClip : generatedTyreSkid;
            tyreSkid.volume = 0f;
            car.interpolation = RigidbodyInterpolation.Interpolate;
            if (terrainStream && terrainStream.HasSource)
            {
                car.position = terrainStream.VehicleSpawnPosition;
                yaw = terrainStream.SpawnHeading;
                car.rotation = Quaternion.Euler(0, yaw, 0);
                player.transform.rotation = car.rotation;
                PlacePlayer(terrainStream.SpawnPosition);
            }
            LockCursor(); UpdateVisibility();
        }
        void LockCursor() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        void OnApplicationFocus(bool focus)
        {
            if (!focus) return;
            if (ModalOpen)
            {
                UnlockCursor();
                if (terminalScreen?.IsOpen == true) terminalScreen.FocusCommand();
            }
            else LockCursor();
        }

        static void UnlockCursor() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

        void Update()
        {
            if (terrainStream) terrainStream.Pump(driving ? car.position : player.transform.position);
            if (WaitingForTerrain) return;
            var keys = Keyboard.current;
            int zoom = (keys?.equalsKey.wasPressedThisFrame == true ? 1 : 0)
                - (keys?.minusKey.wasPressedThisFrame == true ? 1 : 0);
            if (terminalScreen?.IsOpen == true)
            {
                if (!activeTerminal || !activeTerminal.isActiveAndEnabled || keys?.escapeKey.wasPressedThisFrame == true)
                    CloseTerminal();
                return;
            }
            if (fieldReceiver?.IsOpen == true)
            {
                if (keys != null && (keys.iKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame))
                {
                    fieldReceiver.Close();
                    LockCursor();
                }
                return;
            }
            if (worldMap && worldMap.IsOpen)
            {
                if (keys != null && (keys.mKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame))
                {
                    worldMap.Close();
                    LockCursor();
                }
                else if (zoom != 0) worldMap.Zoom(zoom);
                return;
            }
            if (keys != null && keys.iKey.wasPressedThisFrame)
            {
                fieldReceiver ??= new FieldReceiverUI(transform);
                fieldReceiver.Open();
                lastEscape = -10f;
                quitHintUntil = 0;
                if (horn.isPlaying) horn.Stop();
                UnlockCursor();
                return;
            }
            if (keys?.mKey.wasPressedThisFrame == true && worldMap && worldMap.TryOpen())
            {
                if (horn.isPlaying) horn.Stop();
                UnlockCursor();
                return;
            }

            var mouse = Mouse.current;
            if (keys == null) return;
            if (keys.escapeKey.wasPressedThisFrame)
            {
                if (Time.unscaledTime - lastEscape <= .4f)
                {
                    Application.Quit();
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#endif
                }
                lastEscape = Time.unscaledTime;
                quitHintUntil = Time.unscaledTime + 1.5f;
            }
            
            if (keys.backquoteKey.wasPressedThisFrame && !driving) 
            { 
                thirdPerson = !thirdPerson; UpdateVisibility(); 
            }
            
            if (!driving && keys.eKey.wasPressedThisFrame)
            {
                var terminal = Terminal.FindNearby(player.transform.position);
                if (terminal)
                {
                    OpenTerminal(terminal);
                    return;
                }
            }

            if (zoom != 0 && minimap) minimap.Zoom(zoom);

            Vector2 look = mouse != null ? mouse.delta.ReadValue() * .12f : Vector2.zero;
            if (driving && look.sqrMagnitude > 0f) lastDrivingLook = Time.unscaledTime;
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, -75f, 75f);
            if (keys.eKey.wasPressedThisFrame)
            {
                if (driving) TryExit();
                else if (NearCar()) Enter();
            }
            if (!driving)
            {
                player.transform.rotation = Quaternion.Euler(0, yaw, 0);
                Vector2 input = ReadMovement();
                Vector3 movement = player.transform.right * input.x + player.transform.forward * input.y;
                if (player.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
                if (player.isGrounded && keys.spaceKey.wasPressedThisFrame) verticalSpeed = Mathf.Sqrt(2f * 1.25f * 20f);
                verticalSpeed -= 20f * Time.deltaTime;
                player.Move((movement * (ShiftHeld ? WalkSpeed * 1.7f : WalkSpeed) + Vector3.up * verticalSpeed) * Time.deltaTime);
                if (terrainStream && terrainStream.HasSource && player.transform.position.y < terrainStream.RecoveryHeight)
                {
                    terrainStream.RestoreSourceTile();
                    PlacePlayer(terrainStream.SpawnPosition);
                    verticalSpeed = 0;
                }
            }
            if (driving && mouse?.rightButton.isPressed == true) { if (!horn.isPlaying) horn.Play(); }
            else if (horn.isPlaying) horn.Stop();
        }

        void OpenTerminal(Terminal terminal)
        {
            activeTerminal = terminal;
            terminalScreen ??= new TerminalScreen(transform, CloseTerminal);
            lastEscape = -10f;
            quitHintUntil = 0;
            UnlockCursor();
            terminalScreen.Open(terminal.Title);
        }

        void CloseTerminal()
        {
            terminalScreen.Close();
            activeTerminal = null;
            LockCursor();
        }

        void PlacePlayer(Vector3 position)
        {
            player.enabled = false;
            player.transform.position = position;
            player.enabled = true;
        }

        Vector2 ReadMovement()
        {
            var k = Keyboard.current;
            if (k == null) return Vector2.zero;
            return Vector2.ClampMagnitude(new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0), (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0)), 1);
        }

        void InitializeCarMotion()
        {
            if (!car.TryGetComponent(out carMotion))
                carMotion = car.gameObject.AddComponent<CarMotion>();
        }

        void FixedUpdate()
        {
            if (WaitingForTerrain) return;
            if (ModalOpen) return;
            Vector3 focus = driving ? car.position : player.transform.position;
            if (terrainStream && terrainStream.ShiftOriginIfNeeded(focus) && skidMarks != null)
                foreach (var mark in skidMarks) mark.Clear();

            float dt = Time.fixedDeltaTime;
            var keys = Keyboard.current;
            float throttle = driving && keys != null ? Axis(keys.wKey.isPressed, keys.sKey.isPressed) : 0f;
            float steer = driving && keys != null ? Axis(keys.dKey.isPressed, keys.aKey.isPressed) : 0f;
            bool handbrake = !driving || keys?.spaceKey.isPressed == true;
            if (!carMotion) InitializeCarMotion();
            steering = carMotion.StepSteering(steering, steer, dt);

            Vector3 forward = Vector3.ProjectOnPlane(car.rotation * Vector3.forward, Vector3.up).normalized;
            float oldSpeed = Vector3.Dot(carVelocity, forward);
            bool onRoute = carGrounded && terrainStream && terrainStream.IsOnLandRoute(car.position);
            float traction = onRoute ? 1f : carMotion.OffRouteTraction;
            bool boostHeld = driving && ShiftHeld;
            if (carGrounded)
                carVelocity = carMotion.Step(carVelocity, forward, throttle, boostHeld, handbrake, dt, traction);

            float longitudinalSpeed = Vector3.Dot(carVelocity, forward);
            bool underThrottle = driving && !handbrake && throttle != 0f;
            float acceleration = (longitudinalSpeed - oldSpeed) * Mathf.Sign(throttle) / dt;
            engineLoad = underThrottle ? Mathf.Lerp(.2f, 1f, Mathf.InverseLerp(0f, 8f, acceleration)) : 0f;
            boosting = underThrottle && boostHeld;
            onPavement = !terrainStream || !terrainStream.HasSource || onRoute;
            float yawRate = carGrounded ? carMotion.YawRate(steering, longitudinalSpeed, traction, handbrake) : 0f;
            float heading = car.rotation.eulerAngles.y + yawRate * dt;
            Quaternion flat = Quaternion.Euler(0f, heading, 0f);
            Vector3 position = car.position;
            Vector3 next = position + carVelocity * dt;

            // A supported truck carries the vertical speed it gained climbing the slope.
            // At a crest, the ground can fall away faster than gravity pulls the truck down.
            float gravity = Physics.gravity.y;
            float predictedHeight = position.y + (carVerticalSpeed + .5f * gravity * dt) * dt;
            Quaternion rotation = car.rotation;
            if (GroundPose(next, flat, dt, out float supportHeight, out Quaternion supportRotation)
                && carMotion.HasGroundContact(supportHeight, predictedHeight, carGrounded, carVelocity.magnitude))
            {
                next.y = supportHeight;
                rotation = supportRotation;
                carVerticalSpeed = carGrounded ? (supportHeight - position.y) / dt : 0f;
                carGrounded = true;
            }
            else
            {
                next.y = predictedHeight;
                carVerticalSpeed += gravity * dt;
                carGrounded = false;
            }

            if (terrainStream && terrainStream.HasSource && next.y < terrainStream.RecoveryHeight)
            {
                terrainStream.RestoreSourceTile();
                next = terrainStream.VehicleSpawnPosition;
                rotation = Quaternion.Euler(0f, terrainStream.SpawnHeading, 0f);
                carVelocity = Vector3.zero;
                carVerticalSpeed = 0f;
                carGrounded = true;
            }
            else
            {
                ResolveTreeCollision(position, ref next, ref rotation);
            }

            car.MoveRotation(rotation);
            car.MovePosition(next);

            Vector3 headingForward = flat * Vector3.forward;
            float lateralSpeed = Vector3.Dot(carVelocity, flat * Vector3.right);
            float slip = Mathf.Atan2(Mathf.Abs(lateralSpeed), Mathf.Abs(Vector3.Dot(carVelocity, headingForward)) + .1f);
            skidding = driving && carGrounded && carVelocity.sqrMagnitude > 25f
                && Mathf.Abs(lateralSpeed) > 1.25f && slip > 7f * Mathf.Deg2Rad;
            tyreSlip = skidding ? Mathf.InverseLerp(1.25f, 9f, Mathf.Abs(lateralSpeed)) : 0f;
            float pitchTarget = carGrounded ? Mathf.Clamp(-(longitudinalSpeed - oldSpeed) / dt * .35f, -4f, 4f) : 0f;
            float rollTarget = carGrounded ? Mathf.Clamp(yawRate * Mathf.Abs(longitudinalSpeed) * .009f, -6f, 6f) : 0f;
            bodyPitch = Mathf.Lerp(bodyPitch, pitchTarget, 1f - Mathf.Exp(-7f * dt));
            bodyRoll = Mathf.Lerp(bodyRoll, rollTarget, 1f - Mathf.Exp(-7f * dt));
            if (carBody) carBody.localRotation = Quaternion.Euler(bodyPitch, 0f, bodyRoll);
        }

        void ResolveTreeCollision(Vector3 position, ref Vector3 next, ref Quaternion rotation)
        {
            if (!carBox || !carBox.enabled || treeTrunkMask == 0) return;

            Vector3 scale = car.transform.lossyScale;
            Vector3 centerOffset = Vector3.Scale(carBox.center, scale);
            Vector3 halfSize = Vector3.Scale(carBox.size * .5f,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            Quaternion startRotation = car.rotation;
            Vector3 displacement = next - position;
            float distance = displacement.magnitude;

            // The kinematic body needs a sweep because MovePosition follows the requested pose.
            if (distance > .0001f)
            {
                Vector3 direction = displacement / distance;
                if (Physics.BoxCast(position + startRotation * centerOffset, halfSize, direction,
                    out var hit, startRotation, distance + TreeContactGap, treeTrunkMask, QueryTriggerInteraction.Ignore))
                {
                    next = position + direction * Mathf.Clamp(hit.distance - TreeContactGap, 0f, distance);
                    Vector3 horizontalNormal = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
                    float inwardSpeed = Vector3.Dot(carVelocity, horizontalNormal);
                    if (inwardSpeed < 0f) carVelocity -= horizontalNormal * inwardSpeed;
                    if (carGrounded)
                        carVerticalSpeed = (next.y - position.y) / Time.fixedDeltaTime;
                    else if (carVerticalSpeed * hit.normal.y < 0f)
                        carVerticalSpeed *= 1f - hit.normal.y * hit.normal.y;
                }
            }

            // A front or rear corner can reach a trunk as the truck turns at low speed.
            if (Quaternion.Angle(startRotation, rotation) > .01f &&
                Physics.CheckBox(next + rotation * centerOffset, halfSize, rotation,
                    treeTrunkMask, QueryTriggerInteraction.Ignore))
                rotation = startRotation;
        }

        static float Axis(bool positive, bool negative) => (positive ? 1f : 0f) - (negative ? 1f : 0f);

        bool GroundPose(Vector3 next, Quaternion flat, float dt, out float height, out Quaternion rotation)
        {
            height = 0f;
            rotation = car.rotation;
            if (!GroundAt(next, out var center)) return false;

            Vector3 forward = flat * Vector3.forward;
            Vector3 right = flat * Vector3.right;
            Vector3 normal = center.normal;
            // The full footprint supports the chassis at hill crests and on uneven ground.
            if (GroundAt(next + forward * 2.2f, out var f) && GroundAt(next - forward * 2.2f, out var b)
                && GroundAt(next + right * .9f, out var r) && GroundAt(next - right * .9f, out var l))
                normal = Vector3.Cross(f.point - b.point, r.point - l.point).normalized;
            float tiltBlend = 1f - Mathf.Exp(-10f * dt);
            Vector3 up = Vector3.Slerp(car.rotation * Vector3.up, normal, tiltBlend);
            rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, up), up);
            height = center.point.y + .75f;
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                // Approximate the ZIL's axle locations in car-root meters.
                float axleZ = z < 0 ? -1.58f : 2.22f;
                Vector3 corner = rotation * new Vector3(x * .9f, -.7f, axleZ);
                if (GroundAt(next + corner, out var support)) height = Mathf.Max(height, support.point.y - corner.y + .05f);
            }
            return true;
        }

        bool GroundAt(Vector3 point, out RaycastHit ground)
        {
            ground = default;
            float nearest = float.PositiveInfinity;
            int count = Physics.RaycastNonAlloc(point + Vector3.up * 20, Vector3.down, groundHits, 60, carGroundMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (hit.rigidbody == car || hit.collider.transform.IsChildOf(player.transform) || hit.distance >= nearest) continue;
                nearest = hit.distance;
                ground = hit;
            }
            if (nearest == float.PositiveInfinity) return false;
            if (terrainStream && ground.collider is TerrainCollider)
                ground.point += Vector3.up * terrainStream.LandRouteHeightOffset(ground.point);
            return true;
        }

        void LateUpdate()
        {
            UpdateTyreAudio();
            if (carAudio)
            {
                bool active = driving && !ModalOpen && !WaitingForTerrain;
                float speed = carVelocity.magnitude;
                float speedRatio = Mathf.InverseLerp(0f, carMotion.MaxForwardKmh / 3.6f, speed);
                carAudio.UpdateMix(active, !thirdPerson, speed, speedRatio, active ? engineLoad : 0f,
                    boosting, carGrounded, onPavement);
            }
            if (ModalOpen) return;

            if (driving && thirdPerson && Time.unscaledTime - lastDrivingLook > 1.25f)
            {
                float follow = 1f - Mathf.Exp(-3.5f * Time.deltaTime);
                yaw = Mathf.LerpAngle(yaw, car.transform.eulerAngles.y, follow);
                pitch = Mathf.Lerp(pitch, 12f, follow);
            }
            Quaternion look = Quaternion.Euler(pitch, yaw, 0);
            Vector3 anchor = driving ? car.transform.TransformPoint(new Vector3(-.4f, 1.15f, .25f)) : player.transform.position + Vector3.up * .65f;
            Vector3 position = anchor;
            if (thirdPerson)
            {
                Vector3 pivot = driving ? car.transform.position + Vector3.up * 1.9f : anchor + Vector3.up * .35f;
                float distance = driving ? 7 : 4;
                position = pivot - look * Vector3.forward * distance;
                // Ignore the controlled body, but pull the camera in before other obstacles.
                int count = Physics.SphereCastNonAlloc(pivot, .15f, (position - pivot).normalized, cameraHits, distance, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var hit = cameraHits[i];
                    if (hit.collider.transform.IsChildOf(player.transform) || hit.rigidbody == car)
                    {
                        continue;
                    }

                    distance = Mathf.Min(distance, Mathf.Max(.2f, hit.distance - .1f));
                }
                position = pivot - look * Vector3.forward * distance;
            }
            view.transform.SetPositionAndRotation(position, look);
            UpdateSkidMarks();
        }

        bool NearCar() => Vector3.Distance(player.transform.position, car.GetComponent<Collider>().ClosestPoint(player.transform.position)) < 2f;

        bool _bOriginalThirdPersonState = true;
        void Enter()
        {
            driving = true;
            engineLoad = 0f;
            boosting = false;
            player.enabled = false;
            EnsureDriver();
            yaw = car.rotation.eulerAngles.y;
            pitch = 12f;
            lastDrivingLook = Time.unscaledTime;
            _bOriginalThirdPersonState = thirdPerson;
            thirdPerson = true;
            UpdateVisibility();
        }

        void TryExit()
        {
            if (!carGrounded || carVelocity.sqrMagnitude > 4f) return;
            Vector3[] offsets = { Vector3.left * 1.8f, Vector3.right * 1.8f, Vector3.back * 3.3f, Vector3.forward * 3.3f };
            foreach (Vector3 offset in offsets)
            {
                Vector3 candidate = car.transform.TransformPoint(offset);
                if (!Physics.Raycast(candidate + Vector3.up * 4, Vector3.down, out var ground, 10, ~0, QueryTriggerInteraction.Ignore)) continue;
                candidate.y = ground.point.y + 1.05f;
                int count = Physics.OverlapCapsuleNonAlloc(candidate + Vector3.down * .55f, candidate + Vector3.up * .55f, .43f, exitHits, ~0, QueryTriggerInteraction.Ignore);
                bool blocked = false;
                for (int i = 0; i < count; i++)
                {
                    if (!exitHits[i].transform.IsChildOf(player.transform))
                    {
                        blocked = true;
                    }
                }

                if (blocked) continue;
                player.transform.position = candidate;
                player.enabled = true;
                verticalSpeed = 0;
                driving = false;
                carVerticalSpeed = 0f;
                steering = 0f;
                carVelocity = Vector3.zero;
                skidding = false;
                if (!car.isKinematic) car.linearVelocity = Vector3.zero;
                horn.Stop();
                thirdPerson = _bOriginalThirdPersonState;
                UpdateVisibility();
                return;
            }
        }

        void UpdateVisibility()
        {
            playerVisual.enabled = !driving && thirdPerson;
            if (seatedDriver) seatedDriver.enabled = driving && thirdPerson;
            if (seatedTorso) seatedTorso.enabled = driving && !thirdPerson;
        }

        void EnsureDriver()
        {
            if (seatedDriver) return;
            seatedDriver = DriverMesh("Seated Driver Capsule", new Vector3(-.4f, .65f, .15f), new Vector3(.7f, .65f, .7f));
            seatedTorso = DriverMesh("First Person Torso", new Vector3(-.4f, .45f, .15f), new Vector3(.7f, .3f, .7f));
        }

        Renderer DriverMesh(string name, Vector3 position, Vector3 scale)
        {
            var visual = new GameObject(name);
            visual.transform.SetParent(car.transform, false);
            visual.transform.localPosition = position;
            visual.transform.localScale = scale;
            visual.AddComponent<MeshFilter>().sharedMesh = playerVisual.GetComponent<MeshFilter>().sharedMesh;
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = playerVisual.sharedMaterial;
            return renderer;
        }

        void UpdateTyreAudio()
        {
            if (!tyreSkid) return;
            float target = driving && !ModalOpen ? tyreSlip * .5f : 0f;
            tyreSkid.volume = Mathf.MoveTowards(tyreSkid.volume, target, Time.unscaledDeltaTime * 3f);
            if (tyreSkid.volume > 0f && !tyreSkid.isPlaying) tyreSkid.Play();
            else if (tyreSkid.volume == 0f && tyreSkid.isPlaying) tyreSkid.Stop();
        }

        static AudioClip CreateTyreSkidClip()
        {
            const int sampleRate = 22050;
            var samples = new float[sampleRate];
            var random = new System.Random(81);
            float fast = 0f, slow = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float noise = (float)random.NextDouble() * 2f - 1f;
                fast += (noise - fast) * .35f;
                slow += (noise - slow) * .045f;
                float edge = Mathf.Clamp01(Mathf.Min(i, samples.Length - 1 - i) / 80f);
                samples[i] = (fast - slow) * edge * .75f;
            }

            var clip = AudioClip.Create("Generated tyre scrub", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        void UpdateSkidMarks()
        {
            if (skidMarks == null)
            {
                if (!skidding) return;
                skidMarks = new TrailRenderer[2];
                skidMaterial = new Material(playerVisual.sharedMaterial);
                skidMaterial.color = new Color(.12f, .12f, .12f);
                for (int i = 0; i < 2; i++)
                {
                    var mark = new GameObject("Tyre Skid " + i).AddComponent<TrailRenderer>();
                    mark.transform.SetParent(transform, false);
                    mark.time = 15; mark.startWidth = mark.endWidth = .16f;
                    mark.minVertexDistance = .12f; mark.sharedMaterial = skidMaterial;
                    mark.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mark.emitting = false; skidMarks[i] = mark;
                }
            }
            for (int i = 0; i < skidMarks.Length; i++)
            {
                Vector3 point = car.transform.TransformPoint(new Vector3(i == 0 ? -.8f : .8f, 0, -1.65f));
                bool contact = GroundAt(point, out var hit);
                if (contact) skidMarks[i].transform.position = hit.point + hit.normal * .025f;
                skidMarks[i].emitting = skidding && contact;
            }
        }

        void OnDisable()
        {
            if (terminalScreen?.IsOpen == true) CloseTerminal();
            if (fieldReceiver?.IsOpen != true) return;
            fieldReceiver.Close();
            LockCursor();
        }

        void OnDestroy()
        {
            fieldReceiver?.Dispose();
            terminalScreen?.Dispose();
            if (skidMaterial) Destroy(skidMaterial);
            if (tyreSkid) Destroy(tyreSkid);
            if (generatedTyreSkid) Destroy(generatedTyreSkid);
        }

        void OnGUI()
        {
            if (WaitingForTerrain)
            {
                GUI.Box(new Rect(Screen.width / 2 - 100, Screen.height / 2 - 16, 200, 32), "Loading terrain…");
                return;
            }

            if (worldMap && worldMap.IsOpen)
            {
                if (Event.current.type == EventType.Repaint)
                {
                    worldMap.Draw(driving ? car.position : player.transform.position,
                        driving ? car.rotation.eulerAngles.y : yaw);
                }

                return;
            }

            if (ModalOpen)
            {
                return;
            }

            GUI.Box(new Rect(16, 16, 650, 80), driving
                ? "DRIVING   |   W accelerate   ·   S brake/reverse   ·   A/D steer   ·   Shift boost   ·   Space handbrake\nMouse look   ·   RMB horn   ·   E leave   ·   ` camera   ·   Double Esc quit\nI field receiver   ·   M world map   ·   = / - minimap zoom"
                : "WALKING   |   WASD move   ·   Shift sprint   ·   Mouse look   ·   Space jump\nE enter nearby car   ·   ` switch camera   ·   Double Esc quit\nI field receiver   ·   M world map   ·   = / - minimap zoom");

            if (driving)
            {
                GUI.Box(new Rect(16, 104, 200, 30), $"{carVelocity.magnitude * 3.6f:0} km/h" + (skidding ? "   SKIDDING" : ""));
            }
            
            Vector3 position = driving ? car.transform.position : player.transform.position;

            if (Event.current.type == EventType.Repaint)
            {
                if (minimap) minimap.Draw(position, driving ? car.rotation.eulerAngles.y : yaw);
                if (!driving)
                {
                    const float radius = 2.5f;
                    var dot = new Rect(Screen.width * .5f - radius, Screen.height * .5f - radius,
                        radius * 2, radius * 2);
                    GUI.DrawTexture(dot, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, Color.white, 0f, radius);
                }
            }

            if (terrainStream && terrainStream.GetTile(position) is Vector2Int tile)
            {
                GUI.Box(new Rect(16, driving ? 142 : 104, 200, 30), $"Tile ({tile.x}, {tile.y})");
            }

            if (!driving && Terminal.FindNearby(position))
            {
                GUI.Box(new Rect(Screen.width / 2 - 105, Screen.height / 2 + 40, 210, 32), "Press E to Interact");
            }
            else if (!driving && NearCar())
            {
                GUI.Box(new Rect(Screen.width / 2 - 90, Screen.height / 2 + 40, 180, 32), "Press E to enter car");
            }

            if (Time.unscaledTime < quitHintUntil)
            {
                GUI.Box(new Rect(Screen.width / 2 - 150, 110, 300, 32), "Press Esc again quickly to quit");
            }
        }

        bool WaitingForTerrain => terrainStream != null && terrainStream.HasSource && !terrainStream.InitialWindowReady;
        bool ModalOpen => (worldMap && worldMap.IsOpen) || fieldReceiver?.IsOpen == true || terminalScreen?.IsOpen == true;
    }
}
