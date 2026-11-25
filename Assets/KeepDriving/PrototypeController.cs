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
        bool driving, thirdPerson;
        float yaw, pitch, verticalSpeed, speed;
        float lastEscape = -10f, quitHintUntil;
        const float WalkSpeed = 5f;
        readonly Collider[] exitHits = new Collider[16];
        readonly RaycastHit[] groundHits = new RaycastHit[32];
        readonly RaycastHit[] cameraHits = new RaycastHit[32];
        Vector3 carVelocity;
        Renderer seatedDriver, seatedTorso;
        TrailRenderer[] skidMarks;
        Material skidMaterial;
        FieldReceiverUI fieldReceiver;
        TerminalScreen terminalScreen;
        Terminal activeTerminal;
        readonly Minimap minimap = new Minimap();
        [SerializeField] private bool showSpawnedTileOutline = true;
        [SerializeField, Min(0f), Tooltip("Show the yellow outline at this map width in kilometers or closer.")]
        private float spawnedTileOutlineMaxWidthKm = 100f;
        [SerializeField] private bool showSpawnedTileGrid = true;
        [SerializeField, Min(0f), Tooltip("Show the individual tile lines at this map width in kilometers or closer.")]
        private float spawnedTileGridMaxWidthKm = 12.5f;
        readonly WorldMap worldMap = new WorldMap();
        bool skidding;
        bool ShiftHeld => Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);

        void Start()
        {
            car.isKinematic = true;
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
            if (worldMap.IsOpen)
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
            if (keys?.mKey.wasPressedThisFrame == true && terrainStream && terrainStream.HasSource)
            {
                worldMap.Open(terrainStream);
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

            if (!driving && zoom != 0) minimap.Zoom(zoom);

            Vector2 look = mouse != null ? mouse.delta.ReadValue() * .12f : Vector2.zero;
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

        void FixedUpdate()
        {
            if (WaitingForTerrain) return;
            if (ModalOpen) return;
            Vector3 focus = driving ? car.position : player.transform.position;
            if (terrainStream && terrainStream.ShiftOriginIfNeeded(focus) && skidMarks != null)
                foreach (var mark in skidMarks) mark.Clear();

            Vector2 input = driving ? ReadMovement() : Vector2.zero;
            bool braking = !driving || (Keyboard.current != null && Keyboard.current.spaceKey.isPressed);
            Vector3 forward = Vector3.ProjectOnPlane(car.rotation * Vector3.forward, Vector3.up).normalized;
            carVelocity = CarMotion.Step(carVelocity, forward, input.y, driving && ShiftHeld, braking, Time.fixedDeltaTime);
            speed = Vector3.Dot(carVelocity, forward);
            float heading = car.rotation.eulerAngles.y + input.x * 85f * Mathf.Clamp(speed / 5f, -1, 1) * Time.fixedDeltaTime;
            Quaternion flat = Quaternion.Euler(0, heading, 0);
            Vector3 next = car.position + carVelocity * Time.fixedDeltaTime;
            skidding = driving && braking && carVelocity.magnitude > 5;
            if (GroundAt(next, out var center))
            {
                Vector3 normal = center.normal;
                // Sample across the full car footprint so crests do not tip the body abruptly.
                Vector3 front = next + flat * Vector3.forward * 2.2f;
                Vector3 back = next - flat * Vector3.forward * 2.2f;
                Vector3 right = next + flat * Vector3.right * .9f;
                Vector3 left = next - flat * Vector3.right * .9f;
                if (GroundAt(front, out var f) && GroundAt(back, out var b) && GroundAt(right, out var r) && GroundAt(left, out var l))
                    normal = Vector3.Cross(f.point - b.point, r.point - l.point).normalized;
                Quaternion rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(flat * Vector3.forward, normal), normal);
                rotation = Quaternion.Slerp(car.rotation, rotation, 1f - Mathf.Exp(-10f * Time.fixedDeltaTime));
                float height = center.point.y + .75f;
                for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = rotation * new Vector3(x * .9f, -.7f, z * 2.2f);
                        if (GroundAt(next + corner, out var support)) height = Mathf.Max(height, support.point.y - corner.y + .05f);
                    }
                next.y = height;
                car.MoveRotation(rotation);
                car.MovePosition(next);
            }
            else { speed = 0; carVelocity = Vector3.zero; }
        }

        bool GroundAt(Vector3 point, out RaycastHit ground)
        {
            ground = default;
            float nearest = float.PositiveInfinity;
            int count = Physics.RaycastNonAlloc(point + Vector3.up * 20, Vector3.down, groundHits, 60, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (hit.rigidbody == car || hit.collider.transform.IsChildOf(player.transform) || hit.distance >= nearest) continue;
                nearest = hit.distance;
                ground = hit;
            }
            return nearest < float.PositiveInfinity;
        }

        void LateUpdate()
        {
            if (ModalOpen) return;

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
            player.enabled = false;
            EnsureDriver();
            yaw = car.rotation.eulerAngles.y;
            pitch = 0;
            _bOriginalThirdPersonState = thirdPerson;
            thirdPerson = true;
            UpdateVisibility();
        }

        void TryExit()
        {
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
                speed = 0;
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
            minimap.Dispose();
            worldMap.Dispose();
            if (skidMaterial) Destroy(skidMaterial);
        }

        void OnGUI()
        {
            if (WaitingForTerrain)
            {
                GUI.Box(new Rect(Screen.width / 2 - 100, Screen.height / 2 - 16, 200, 32), "Loading terrain…");
                return;
            }

            if (worldMap.IsOpen)
            {
                if (Event.current.type == EventType.Repaint)
                {
                    bool showTileOutline = worldMap.WidthKm <= spawnedTileOutlineMaxWidthKm;
                    bool showTileGrid = worldMap.WidthKm <= spawnedTileGridMaxWidthKm;

                    // Draw(TerrainStream stream, Vector3 player, bool showSpawnedTileOutline, bool showSpawnedTileGrid, float heading)
                    worldMap.Draw(terrainStream, 
                        driving ? car.position : player.transform.position,
                        showTileOutline,
                        showTileGrid,
                        driving ? car.rotation.eulerAngles.y : yaw);
                }

                return;
            }

            if (ModalOpen)
            {
                return;
            }

            GUI.Box(new Rect(16, 16, 650, 80), driving
                ? "DRIVING   |   WASD drive   ·   Shift faster   ·   Space brake/skid\nMouse look   ·   RMB horn   ·   E leave   ·   ` camera   ·   Double Esc quit\nI field receiver   ·   M world map"
                : "WALKING   |   WASD move   ·   Shift sprint   ·   Mouse look   ·   Space jump\nE enter nearby car   ·   ` switch camera   ·   Double Esc quit\nI field receiver   ·   M world map   ·   = / - minimap zoom");

            if (driving)
            {
                GUI.Box(new Rect(16, 104, 200, 30), $"{carVelocity.magnitude * 3.6f:0} km/h" + (skidding ? "   SKIDDING" : ""));
            }
            
            Vector3 position = driving ? car.transform.position : player.transform.position;

            if (Event.current.type == EventType.Repaint)
            {
                minimap.Draw(gameObject.scene, position, driving ? car.rotation.eulerAngles.y : yaw, terrainStream);
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
        bool ModalOpen => worldMap.IsOpen || fieldReceiver?.IsOpen == true || terminalScreen?.IsOpen == true;
    }
}
