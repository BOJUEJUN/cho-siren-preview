using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class FontRasterization1028Tests
    {
        [Test]
        public void SmallChineseTypeKeepsDynamicDensityAndPixelAlignedStrokes()
        {
            const string path = "Assets/Resources/Fonts/NotoSansSC-Subset.otf";
            var importer = (TrueTypeFontImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.fontRenderingMode, Is.EqualTo(FontRenderingMode.HintedSmooth));
            Assert.That(AssetDatabase.LoadAssetAtPath<Font>(path).dynamic, Is.True,
                "glyphs must be generated for the actual screen scale rather than upscaling a fixed 16px atlas");
        }
    }
}
