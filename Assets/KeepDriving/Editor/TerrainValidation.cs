using UnityEngine;
using UnityEditor;

namespace KeepDriving.Editor
{
    public static class TerrainValidation
    {
        public static void ConvertAndValidate()
        {
            PrototypeTerrain.Upgrade();
            var terrain = Object.FindFirstObjectByType<Terrain>();
            Check(terrain && terrain.terrainData, "Native Terrain and TerrainData");
            Check(terrain.GetComponent<TerrainCollider>().terrainData == terrain.terrainData, "TerrainCollider shares data");
            Check(!terrain.GetComponent<MeshFilter>() && !terrain.GetComponent<MeshCollider>(), "Old mesh components removed");
            var data = terrain.terrainData;
            Check(data.terrainLayers.Length == 2 && data.terrainLayers[0].diffuseTexture && data.terrainLayers[1].diffuseTexture, "Two paintable textured layers");
            Check(data.heightmapResolution == 513 && data.size == new Vector3(500, 40, 500), "Editable 500m heightmap");
            foreach (var p in new[] { Vector3.zero, new Vector3(0, 0, 45), new Vector3(45, 0, 55), new Vector3(-45, 0, 65) })
            {
                float sampled = terrain.SampleHeight(p);
                Check(Mathf.Abs(sampled - PrototypeTerrain.Height(p.x, p.z)) < .08f, "Original hill elevations preserved");
                bool terrainHit = false;
                foreach (var hit in Physics.RaycastAll(p + Vector3.up * 30, Vector3.down, 40))
                    if (hit.collider is TerrainCollider && Mathf.Abs(hit.point.y - sampled) < .08f) terrainHit = true;
                Check(terrainHit, "Native terrain collision");
            }
            // Exercise Unity's paint storage, then restore the original grass coverage.
            float[,,] original = data.GetAlphamaps(250, 250, 1, 1);
            var brush = new float[1, 1, 2]; brush[0, 0, 1] = 1;
            data.SetAlphamaps(250, 250, brush);
            Check(data.GetAlphamaps(250, 250, 1, 1)[0, 0, 1] > .99f, "Texture painting works");
            data.SetAlphamaps(250, 250, original);
            EditorUtility.SetDirty(data); AssetDatabase.SaveAssets();
            DrivingValidation.Validate();
            Debug.Log("NATIVE_TERRAIN_VALIDATION_PASSED: native terrain, hills, TerrainCollider, paint layers, painting, driving, braking, slope traversal");
        }
        static void Check(bool result, string label) { if (!result) throw new System.Exception("Terrain validation failed: " + label); }
    }
}
