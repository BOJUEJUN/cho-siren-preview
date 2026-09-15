using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    public sealed partial class ChoSirenApp
    {
        private static Sprite navigationBackdrop038;

        private void BuildSharedNavigationBackdrop038()
        {
            const int height = 246;
            const int fadeHeight = 28;
            if (navigationBackdrop038 == null)
            {
                // This is a UI surface, not an edit of the approved illustration.
                // It becomes fully opaque before the old painted navigation begins,
                // removing both duplicate icons and the permanent painted lobby line.
                var texture = new Texture2D(1, height, TextureFormat.RGBA32, false);
                texture.name = "SharedNavigationBackdrop038";
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                var pixels = new Color[height];
                for (int y = 0; y < height; y++)
                {
                    Color color = Color.Lerp(new Color32(8, 7, 30, 255),
                        new Color32(24, 12, 53, 255), y / (height - 1f));
                    color.a = Mathf.Clamp01((height - 1f - y) / fadeHeight);
                    pixels[y] = color;
                }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                texture.hideFlags = HideFlags.DontSave;
                navigationBackdrop038 = Sprite.Create(texture, new Rect(0, 0, 1, height),
                    new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
                navigationBackdrop038.hideFlags = HideFlags.DontSave;
            }
            GameObject backdrop = NewImage("SharedNavigationBackdrop038", navRoot,
                navigationBackdrop038, Color.white);
            // navRoot starts at screen y=1318; fade starts at1290 and coverage ends1536.
            PlaceTop(backdrop.GetComponent<RectTransform>(), -12, -28, 720, height);
            Image image = backdrop.GetComponent<Image>();
            image.preserveAspect = false;
            image.raycastTarget = false;
            backdrop.transform.SetAsFirstSibling();
        }
    }
}
