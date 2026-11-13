using System.Collections.Generic;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    /// <summary>
    /// 2026-11-12 第三章 顶盾: a row of slanted yellow-green shield pieces with a red rim under the boss health bar, one per
    /// layer (同事参考: 血条下面那排斜盾片). A cracked layer (one of its two hits taken) shows half bright.
    /// </summary>
    public sealed partial class TacticsBattlePanel
    {
        private RectTransform shieldPipRow1111;
        private readonly List<Image> shieldPips1111 = new List<Image>();
        private static Sprite shieldPipSprite1111;

        private void RefreshShieldPips1111(BattleUnit boss)
        {
            int max = boss != null ? battle.BarrierMax(boss) : 0;
            if (max <= 0 || enemyHpFill == null)
            {
                if (shieldPipRow1111 != null) shieldPipRow1111.gameObject.SetActive(false);
                return;
            }
            if (shieldPipRow1111 == null)
            {
                shieldPipRow1111 = kit.NewRect("ShieldPips1111", enemyHpFill.transform.parent);
                shieldPipRow1111.anchorMin = shieldPipRow1111.anchorMax = shieldPipRow1111.pivot = new Vector2(0f, 1f);
                RectTransform bar = enemyHpFill.rectTransform;
                Vector3[] corners = new Vector3[4];
                bar.GetWorldCorners(corners);
                Vector3 local = shieldPipRow1111.parent.InverseTransformPoint(corners[0]); // bar's bottom-left
                var parentRect = (RectTransform)shieldPipRow1111.parent;
                shieldPipRow1111.anchoredPosition = new Vector2(local.x - parentRect.rect.xMin, local.y - parentRect.rect.yMax - 4f);
                shieldPipRow1111.sizeDelta = new Vector2(320f, 28f);
            }
            shieldPipRow1111.gameObject.SetActive(true);
            while (shieldPips1111.Count < max)
            {
                Image pip = kit.NewImage("ShieldPip", shieldPipRow1111, PipSprite1111(), Color.white);
                pip.raycastTarget = false;
                PanelKit.PlaceTop(pip.rectTransform, shieldPips1111.Count * 36f, 0f, 34f, 27f);
                shieldPips1111.Add(pip);
            }
            int layers = battle.Barrier(boss), cracked = battle.BarrierLayerHits(boss);
            for (int i = 0; i < shieldPips1111.Count; i++)
            {
                shieldPips1111[i].gameObject.SetActive(i < max);
                bool up = i < layers;
                bool crackedPip = up && i == layers - 1 && cracked > 0;
                shieldPips1111[i].color = !up ? new Color32(70, 60, 80, 140)
                    : crackedPip ? new Color32(210, 230, 120, 200) : new Color32(255, 255, 255, 255);
            }
        }

        /// <summary>Slanted yellow-green plate with a red rim (procedural until authored art arrives).</summary>
        private static Sprite PipSprite1111()
        {
            if (shieldPipSprite1111 != null) return shieldPipSprite1111;
            const int w = 44, h = 36, slant = 10, rim = 3;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "ShieldPip1111", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float shift = slant * (y / (float)(h - 1));
                for (int x = 0; x < w; x++)
                {
                    float left = shift, right = w - 1 - slant + shift;
                    bool inside = x >= left && x <= right;
                    bool edge = inside && (x < left + rim || x > right - rim || y < rim || y >= h - rim);
                    px[y * w + x] = !inside ? new Color32(0, 0, 0, 0)
                        : edge ? new Color32(214, 38, 52, 255) : new Color32(196, 232, 72, 255);
                }
            }
            texture.SetPixels32(px);
            texture.Apply(false, true);
            shieldPipSprite1111 = Sprite.Create(texture, new Rect(0, 0, w, h), Vector2.one * .5f, 100f);
            return shieldPipSprite1111;
        }
    }
}
