using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KeepDriving.Editor
{
    public static class PrototypeTerrain
    {
        public static float Height(float x, float z)
        {
            // The spawn area stays flat; broad rounded hills have gentle approaches on every side.
            float fade = Mathf.SmoothStep(0, 1, Mathf.Clamp01((new Vector2(x, z).magnitude - 10) / 12));
            return fade * (Hill(x, z, 0, 45, 18, 22, 7) + Hill(x, z, 45, 55, 20, 28, 12) + Hill(x, z, -45, 65, 24, 30, 17));
        }
        static float Hill(float x, float z, float cx, float cz, float sx, float sz, float height)
            => height * Mathf.Exp(-((x-cx)*(x-cx)/(sx*sx) + (z-cz)*(z-cz)/(sz*sz)));

        public static void Configure(GameObject ground)
        {
            // Preserve future hand sculpting/painting if this setup is run again.
            if (ground.GetComponent<Terrain>() && ground.GetComponent<Terrain>().terrainData) return;
            const int size = 513;
            const string path = "Assets/KeepDriving/PaintableTerrain.asset";
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (!data)
            {
                data = new TerrainData { name = "Paintable Rolling Hills" };
                data.heightmapResolution = size;
                data.size = new Vector3(500, 40, 500);
                data.alphamapResolution = 512;
                var heights = new float[size, size];
                for (int z = 0; z < size; z++)
                    for (int x = 0; x < size; x++)
                        heights[z, x] = Height(x * 500f / (size - 1) - 250, z * 500f / (size - 1) - 250) / data.size.y;
                data.SetHeights(0, 0, heights);
                data.terrainLayers = new[] { Layer("Grass", new Color(.32f, .39f, .29f)), Layer("Dirt", new Color(.43f, .32f, .21f)) };
                var paint = new float[512, 512, 2];
                for (int z = 0; z < 512; z++) for (int x = 0; x < 512; x++) paint[z, x, 0] = 1;
                data.SetAlphamaps(0, 0, paint);
                AssetDatabase.CreateAsset(data, path);
            }
            foreach (var collider in ground.GetComponents<Collider>()) Object.DestroyImmediate(collider);
            if (ground.GetComponent<MeshRenderer>()) Object.DestroyImmediate(ground.GetComponent<MeshRenderer>());
            if (ground.GetComponent<MeshFilter>()) Object.DestroyImmediate(ground.GetComponent<MeshFilter>());
            ground.name = "Terrain (Paintable Rolling Hills)";
            ground.transform.SetPositionAndRotation(new Vector3(-250, 0, -250), Quaternion.identity);
            ground.transform.localScale = Vector3.one;
            var terrain = ground.GetComponent<Terrain>() ?? ground.AddComponent<Terrain>();
            terrain.terrainData = data;
            terrain.heightmapPixelError = 3;
            terrain.drawInstanced = true;
            const string materialPath = "Assets/KeepDriving/TerrainSurface.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material)
            {
                var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
                if (!shader) throw new System.Exception("URP Terrain Lit shader missing");
                material = new Material(shader) { name = "Terrain Surface" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            terrain.materialTemplate = material;
            ground.AddComponent<TerrainCollider>().terrainData = data;
            terrain.Flush();
        }

        static TerrainLayer Layer(string name, Color color)
        {
            string path = "Assets/KeepDriving/" + name + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer) return layer;
            string texturePath = "Assets/KeepDriving/" + name + "Terrain.png";
            var texture = new Texture2D(64, 64, TextureFormat.RGB24, false);
            var pixels = new Color[64 * 64];
            for (int z = 0; z < 64; z++) for (int x = 0; x < 64; x++)
                pixels[z * 64 + x] = color * (.9f + .2f * Mathf.PerlinNoise(x * .2f, z * .2f));
            texture.SetPixels(pixels); texture.Apply();
            System.IO.File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            layer = new TerrainLayer { name = name, diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath), tileSize = new Vector2(8, 8), metallic = 0, smoothness = 0 };
            AssetDatabase.CreateAsset(layer, path);
            return layer;
        }

        [MenuItem("Keep Driving/Convert Ground To Native Terrain")]
        public static void Upgrade()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/KeepDriving.unity");
            var ground = GameObject.Find("Terrain (Paintable Rolling Hills)") ?? GameObject.Find("Flat Ground (500m x 500m)") ?? GameObject.Find("Rolling Terrain (500m x 500m)");
            if (!ground) throw new System.Exception("Prototype ground missing");
            Configure(ground);
            var game = Object.FindFirstObjectByType<PrototypeController>();
            game.car.isKinematic = true;
            game.car.interpolation = RigidbodyInterpolation.Interpolate;
            game.car.constraints = RigidbodyConstraints.None;
            EditorSceneManager.SaveScene(game.gameObject.scene);
            AssetDatabase.SaveAssets();
            PrototypeValidation.Validate();
            Debug.Log("KEEP_DRIVING_SLOPES_READY");
        }
    }
}
