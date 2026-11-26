using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace KeepDriving.Editor
{
    public sealed class MinimapBake : IPreprocessBuildWithReport
    {
        private const string Input = "Assets/MapData/";
        private const string Output = "Assets/StreamingAssets/YanorraMinimap.bytes";
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => Bake();

        [MenuItem("KeepDriving/Bake Minimap Relief")]
        public static void Bake()
        {
            WorldMap.BakeMinimapArchive(
                AssetDatabase.LoadAssetAtPath<TextAsset>(Input + "elevation_control_points.geojson"),
                AssetDatabase.LoadAssetAtPath<TextAsset>(Input + "elevation.bytes"),
                AssetDatabase.LoadAssetAtPath<TextAsset>(Input + "biomes.geojson"),
                Path.GetFullPath(Output));
            AssetDatabase.ImportAsset(Output);
            Debug.Log($"Baked minimap relief: {new FileInfo(Output).Length / 1048576.0:F1} MiB.");
        }
    }
}
