using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Run after generic small-download importers. Native readers use these imported textures,
// whereas the web reader already loads the original scene/board files from StreamingAssets.
public sealed class NativePortraitQuality1028 : AssetPostprocessor
{
    public override int GetPostprocessOrder() => 1000;
    // Not bumped for the 2026-11-01 WebGL rule: a version change reimports every texture in the project.
    // The 41 home-layer .meta files carry the same WebGL override, so only they reimport.
    public override uint GetVersion() => 1;

    public static bool Applies(string path) =>
        path.StartsWith("Assets/Resources/Art/Reference038/", StringComparison.Ordinal) ||
        path.StartsWith("Assets/Resources/Art/Story/Scenes1019/", StringComparison.Ordinal) ||
        new[] { "xingli", "feiyin", "wubai", "yeying" }.Any(id => path == "Assets/Resources/Art/Members/member-" + id + ".png");

    void OnPreprocessTexture()
    {
        if (!Applies(assetPath)) return;
        var importer = (TextureImporter)assetImporter;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        // BC7 keeps alpha and fine colour gradients without Crunch's extra lossy step.
        Configure(importer, "Standalone", TextureImporterFormat.BC7);
        Configure(importer, "Android", TextureImporterFormat.ASTC_4x4);
        // The lobby fills a Retina screen. Keep the authored portrait resolution on iOS too,
        // without changing the import budget for every other reference/UI texture.
        // 2026-11-01 video-hq: every homepage layer, not only the idols — without an iOS override the other 33 layers
        // used the Default settings (2048 cap, Crunch), which shrink the 1472×3077 backdrop on iPhone.
        if (IsHomeLayer(assetPath))
            Configure(importer, BuildPipeline.GetBuildTargetName(BuildTarget.iOS), TextureImporterFormat.ASTC_4x4);
        // 2026-11-01 (赵金 "资源不清楚"): the WebGL player never ships these imports — ChoSirenBuild moves
        // Reference038 out of Resources and ReferenceArt038 streams the original PNG bytes, decoded at full size
        // into RGBA32. Without an override the editor (and every PlayMode capture run with -buildTarget WebGL)
        // previewed the homepage through the default 2048 cap + Crunch, i.e. blurrier than the real web page.
        // Mirror what the browser actually decodes for every homepage layer.
        if (IsHomeLayer(assetPath))
            Configure(importer, "WebGL", TextureImporterFormat.RGBA32);
    }

    static bool IsHomeLayer(string path) =>
        path.StartsWith("Assets/Resources/Art/Reference038/home-layer-", StringComparison.Ordinal);

    static void Configure(TextureImporter importer, string platform, TextureImporterFormat format)
    {
        var settings = importer.GetPlatformTextureSettings(platform);
        settings.overridden = true;
        settings.maxTextureSize = 4096;
        settings.format = format;
        settings.textureCompression = TextureImporterCompression.CompressedHQ;
        settings.crunchedCompression = false;
        settings.compressionQuality = 100;
        importer.SetPlatformTextureSettings(settings);
    }
}
