using UnityEditor;
using UnityEngine;

/// <summary>
/// Blender-exported props: the FBX units are mislabelled, so Unity's file scale shrinks them. Ignore the file scale, bake the axis
/// rotation into the mesh (clean identity root), and apply one known scale so a pen is a pen-sized object in metres.
/// </summary>
public sealed class PropImportSettings : AssetPostprocessor
{
    public const string Folder = "Assets/_Breathe/Art/Props/";
    public static float GlobalScale = 10f;   // measured: design millimetres -> metres (a 126 mm barrel must be 0.126 m)
    void OnPreprocessModel()
    {
        if(!assetPath.StartsWith(Folder)) return;
        var importer=(ModelImporter)assetImporter;
        importer.useFileScale=false; importer.bakeAxisConversion=true; importer.globalScale=GlobalScale;
        importer.importCameras=false; importer.importLights=false;
        // OfficeProps keep their material names (<Part>_main/_dark/_metal/_glow/_accent/_glass) so the game can re-skin them per slot; older kits import without materials.
        if(assetPath.StartsWith(Folder+"OfficeProps/")) { importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard; importer.materialLocation=ModelImporterMaterialLocation.InPrefab; }
        else importer.materialImportMode=ModelImporterMaterialImportMode.None;
    }
}
