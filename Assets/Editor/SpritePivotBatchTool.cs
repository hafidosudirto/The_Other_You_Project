using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class SpritePivotBatchTool
{
    [MenuItem("Tools/Sprites/Set Selected Texture Pivot To Custom")]
    public static void SetPivotForSelectedTexture()
    {
        var texture = Selection.activeObject as Texture2D;
        if (texture == null)
        {
            EditorUtility.DisplayDialog("Sprite Pivot Batch",
                "Pilih dulu file tekstur sprite sheet di Project.", "OK");
            return;
        }

        string path = AssetDatabase.GetAssetPath(texture);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            EditorUtility.DisplayDialog("Sprite Pivot Batch",
                "Asset yang dipilih bukan TextureImporter.", "OK");
            return;
        }

        // Nilai pivot contoh:
        // karena Pivot Unit Mode biasanya Normalized, rentangnya 0..1
        Vector2 customPivot = new Vector2(0.7710199f, 0.517924f);

        var factory = new SpriteDataProviderFactories();
        factory.Init();

        var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        dataProvider.InitSpriteEditorDataProvider();

        var rects = dataProvider.GetSpriteRects();

        for (int i = 0; i < rects.Length; i++)
        {
            rects[i].alignment = SpriteAlignment.Custom;
            rects[i].pivot = customPivot;
        }

        dataProvider.SetSpriteRects(rects);
        dataProvider.Apply();

        importer.SaveAndReimport();

        EditorUtility.DisplayDialog("Sprite Pivot Batch",
            $"Berhasil menerapkan pivot {customPivot} ke {rects.Length} sprite.", "OK");
    }
}