using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KeepDriving.Editor
{
    [InitializeOnLoad]
    public static class PrototypeSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/KeepDriving.unity";
        static PrototypeSceneBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (!Application.isBatchMode && !EditorApplication.isPlayingOrWillChangePlaymode && !System.IO.File.Exists(ScenePath)) Build();
            };
        }
        [MenuItem("Keep Driving/Create Prototype Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var ground = new GameObject("Terrain (Paintable Rolling Hills)");
            var terrainStream = PrototypeTerrain.Configure(ground);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 2;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientLight = new Color(.55f, .6f, .68f);
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player Capsule (1.8m)";
            player.transform.position = new Vector3(0, 1, -5);
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = .35f; controller.stepOffset = .3f;
            player.GetComponent<Renderer>().sharedMaterial = Material("Player", new Color(.8f, .65f, .25f));
            var car = GameObject.CreatePrimitive(PrimitiveType.Cube);
            car.name = "Car (1.8m wide x 4.4m long)";
            car.transform.position = new Vector3(0, .75f, 0);
            // Keep the physics root at unit scale so seat/exit offsets remain in metres.
            car.GetComponent<BoxCollider>().size = new Vector3(1.8f, 1.4f, 4.4f);
            Object.DestroyImmediate(car.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(car.GetComponent<MeshFilter>());
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Rectangle Body"; body.transform.SetParent(car.transform, false);
            body.transform.localScale = new Vector3(1.8f, 1.4f, 4.4f);
            Object.DestroyImmediate(body.GetComponent<BoxCollider>());
            body.GetComponent<Renderer>().sharedMaterial = Material("Car", new Color(.12f, .35f, .65f));
            var rb = car.AddComponent<Rigidbody>();
            rb.mass = 1200; rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.None;
            var audio = car.AddComponent<AudioSource>();
            audio.playOnAwake = false; audio.loop = true; audio.volume = .35f; audio.spatialBlend = 0;
            const int rate = 44100;
            var clip = AudioClip.Create("Horn", rate, 1, rate, false);
            float[] samples = new float[rate];
            for (int i = 0; i < samples.Length; i++) samples[i] = .4f * (Mathf.Sin(2 * Mathf.PI * 330 * i / rate) + .5f * Mathf.Sin(2 * Mathf.PI * 440 * i / rate));
            clip.SetData(samples, 0);
            // Generated clips must be reconstructed at runtime; a small PCM WAV persists in builds.
            WriteHorn(samples, rate);
            AssetDatabase.ImportAsset("Assets/KeepDriving/Horn.wav", ImportAssetOptions.ForceSynchronousImport);
            audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/KeepDriving/Horn.wav");
            Object.DestroyImmediate(clip);
            var camera = new GameObject("Player Camera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.nearClipPlane = .05f; camera.farClipPlane = 700;
            camera.transform.position = player.transform.position + Vector3.up * .65f;
            camera.gameObject.AddComponent<AudioListener>();
            var game = new GameObject("Prototype Controller").AddComponent<PrototypeController>();
            game.terrainStream = terrainStream;
            game.player = controller; game.playerVisual = player.GetComponent<Renderer>(); game.car = rb; game.view = camera; game.horn = audio;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("KEEP_DRIVING_SCENE_READY");
        }
        static Material Material(string name, Color color)
        {
            string path = "Assets/KeepDriving/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.color = color;
            return material;
        }
        static void WriteHorn(float[] samples, int rate)
        {
            using (var stream = System.IO.File.Create("Assets/KeepDriving/Horn.wav"))
            using (var writer = new System.IO.BinaryWriter(stream))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
                foreach (float sample in samples) writer.Write((short)(sample * short.MaxValue));
            }
        }
    }
}
