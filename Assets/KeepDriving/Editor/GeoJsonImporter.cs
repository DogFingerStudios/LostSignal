using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

[ScriptedImporter(1, "geojson")]
public sealed class GeoJsonImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext context)
    {
        var asset = new TextAsset(File.ReadAllText(context.assetPath));
        asset.name = Path.GetFileNameWithoutExtension(context.assetPath);
        context.AddObjectToAsset("geojson", asset);
        context.SetMainObject(asset);
    }
}
