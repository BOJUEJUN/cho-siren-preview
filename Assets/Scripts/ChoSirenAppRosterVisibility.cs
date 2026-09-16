using ChoSiren.Panels;
using ChoSiren.Systems.Presentation;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Locked-roster presentation. Owned members use the existing full card/profile; members the
    /// player has not signed only ever get a flat silhouette plus progress copy. Keeping both
    /// locked builders here means the reveal rules stay in one small partial.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        // One flat colour + one procedural bust shape for every locked member. Both the grid
        // card and the profile portrait read these, so the two screens can never diverge.
        private static readonly Color LockedSilhouetteTint = new Color(
            MemberRosterVisibility.SilhouetteRed, MemberRosterVisibility.SilhouetteGreen,
            MemberRosterVisibility.SilhouetteBlue, MemberRosterVisibility.SilhouetteAlpha);
        private static readonly Color LockedSilhouetteMark = new Color(0.62f, 0.66f, 0.84f, 0.85f);
        private Sprite lockedSilhouetteSprite;

        /// <summary>
        /// Generic head-and-shoulders bust generated once at runtime. It deliberately samples no
        /// member texture: the output is a white alpha mask that callers tint with the single flat
        /// LockedSilhouetteTint, so a locked card/profile can never render real artwork detail.
        /// </summary>
        private Sprite LockedSilhouetteSprite()
        {
            if (lockedSilhouetteSprite != null) return lockedSilhouetteSprite;

            const int width = 160;
            const int height = 240;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = MemberRosterVisibility.SilhouetteSpriteName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;

                    // The shape lives in MemberRosterVisibility so the source-built checks cover it.
                    float coverage = MemberRosterVisibility.SilhouetteCoverage(px, py);
                    pixels[y * width + x] = coverage <= 0.001f
                        ? new Color32(0, 0, 0, 0)
                        : new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            lockedSilhouetteSprite = Sprite.Create(texture, new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            lockedSilhouetteSprite.name = texture.name;
            lockedSilhouetteSprite.hideFlags = HideFlags.DontSave;
            return lockedSilhouetteSprite;
        }

        /// <summary>
        /// Roster grid card for a not-yet-signed member: silhouette + remaining count only.
        /// RosterPolish038: the whole block is lowered into a balanced composition and the
        /// authored glitch silhouette is clipped to the card's slanted window so no
        /// rectangular seam crosses the jagged frame. The procedural bust stays as the
        /// direct "LockedSilhouette" child — it is the reveal contract and the fallback
        /// whenever the authored art is missing.
        /// </summary>
        private void BuildLockedMemberCard(GameObject card, int width, int height)
        {
            GameObject glow = NewImage("CareerGlow", card.transform, StageGlowSprite(),
                new Color32(96, 108, 168, 40));
            PlaceTop(glow.GetComponent<RectTransform>(), 3, 3, width - 6,
                Mathf.RoundToInt(height * .62f));

            // Balanced vertical rhythm: ~9% top margin, ~55% window, labels centred below.
            float wx = 6f, ww = width - 12f;
            float wy = Mathf.Round(height * .085f);
            float wh = Mathf.Round(height * .55f);

            GameObject silhouette = NewImage("LockedSilhouette", card.transform, LockedSilhouetteSprite(),
                LockedSilhouetteTint);
            PlaceTop(silhouette.GetComponent<RectTransform>(), wx, wy, ww, wh);
            silhouette.GetComponent<Image>().preserveAspect = true;
            silhouette.GetComponent<Image>().raycastTarget = false;

            Text mark = NewPlacedText(card.transform, MemberRosterVisibility.LockedSilhouetteMark, 40,
                LockedSilhouetteMark, wx, wy + wh * .30f, ww, 52, TextAnchor.MiddleCenter, FontStyle.Bold);
            mark.name = "LockedMark";

            Sprite art = ReferenceArt038.Load("Art/Reference038/unknown-member-038");
            if (art != null)
            {
                GameObject clip = RosterPolish038Clip(card.transform, "RosterPolish038Clip",
                    wx, wy, ww, wh);
                GameObject image = NewImage("RosterPolish038LockedArt", clip.transform, art, White);
                // Cover-fit: fill the window width and let the clip take the slanted edges.
                float artHeight = ww * art.rect.height / Mathf.Max(1f, art.rect.width);
                PlaceTop(image.GetComponent<RectTransform>(), 0, 0, ww, Mathf.Max(wh, artHeight));
                image.GetComponent<Image>().raycastTarget = false;
                glow.SetActive(false);
                mark.gameObject.SetActive(false);
            }

            float labelY = wy + wh + 8f;
            Text name = NewPlacedText(card.transform, MemberRosterVisibility.LockedName, 16,
                new Color32(198, 192, 226, 255), 8, labelY, width - 16, 26,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            name.name = "LockedName";
            PanelKit.EnableBestFit(name, 13);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            Text progress = NewPlacedText(card.transform,
                MemberRosterVisibility.LockedCardTitle(MemberRosterVisibility.RemainingCount(
                    model.Save.UnlockedMembers.Count, GameModel.Members.Length)),
                11, Muted, 8, labelY + 28f, width - 16, 22, TextAnchor.MiddleCenter, FontStyle.Bold);
            progress.name = "LockedProgress";
            PanelKit.EnableBestFit(progress, 10);
            progress.horizontalOverflow = HorizontalWrapMode.Overflow;
            progress.verticalOverflow = VerticalWrapMode.Truncate;

            Outline edge = card.AddComponent<Outline>();
            edge.effectColor = new Color32(95, 111, 165, 58);
            edge.effectDistance = new Vector2(1f, -1f);
        }

        /// <summary>Profile portrait slot for a locked member: the same flat generic silhouette.</summary>
        private void BuildLockedMemberPortrait(GameObject panel)
        {
            // Angular crystal plate (ModalPolish038 family) instead of the rounded panel —
            // same slot/name children so the locked reveal contract and tests stay intact.
            GameObject frame = ModalPolish038Chrome.RowPlate(panel.transform, "LockedPortraitFrame");
            RectTransform frameRect = frame.GetComponent<RectTransform>();
            PlaceTop(frameRect, 24, 52, 292, 390);
            ReferencePunkFx.Tilt(frameRect, -2.5f);

            GameObject portrait = NewImage("Portrait", frame.transform, LockedSilhouetteSprite(),
                LockedSilhouetteTint);
            Stretch(portrait.GetComponent<RectTransform>(), 8, 8, -8, -8);
            Image portraitImage = portrait.GetComponent<Image>();
            portraitImage.preserveAspect = true;
            portraitImage.useSpriteMesh = true;
            portraitImage.raycastTarget = false;

            Text mark = NewPlacedText(frame.transform, MemberRosterVisibility.LockedSilhouetteMark, 92,
                LockedSilhouetteMark, 8, 130, 276, 130, TextAnchor.MiddleCenter, FontStyle.Bold);
            mark.name = "LockedSilhouetteMark";
            Text hint = NewPlacedText(frame.transform, MemberRosterVisibility.LockedCardHint, 15,
                Muted, 8, 344, 276, 30, TextAnchor.MiddleCenter, FontStyle.Bold);
            hint.name = "LockedPortraitHint";
        }

        /// <summary>Locked profile body: progress + acquisition route, and nothing unrevealed.</summary>
        private void BuildLockedMemberProfileBody(GameObject panel, int memberIndex)
        {
            int owned = model.Save.UnlockedMembers.Count;
            int total = GameModel.Members.Length;

            GameObject guidePanel = ModalPolish038Chrome.RowPlate(panel.transform,
                "MemberAcquireGuide");
            PlaceTop(guidePanel.GetComponent<RectTransform>(), 28, 458, 564, 300);
            NewPlacedText(guidePanel.transform, "获取方式", 15, new Color32(255, 188, 231, 255),
                18, 12, 520, 26, TextAnchor.MiddleLeft, FontStyle.Bold);
            Text progress = NewPlacedText(guidePanel.transform,
                MemberRosterVisibility.LockedProfileProgress(owned, total), 22, Cyan,
                18, 46, 520, 36, TextAnchor.MiddleLeft, FontStyle.Bold);
            progress.name = "MemberLockedProgress";
            PanelKit.EnableBestFit(progress, 16);
            NewPlacedText(guidePanel.transform, MemberRosterVisibility.LockedProfileHint, 16, White,
                18, 92, 520, 62, TextAnchor.UpperLeft);
            NewPlacedText(guidePanel.transform,
                "已获得角色可在成员档案中查看完整形象、舞台四维、普通攻击、主动技能与队长特性；\n未获得角色只显示剪影和剩余数量，不展示未公开内容。",
                14, Muted, 18, 166, 520, 80, TextAnchor.UpperLeft);
            NewPlacedText(guidePanel.transform,
                $"候选每日 18:00 刷新 · 当前已拥有 {owned}/{total}",
                13, new Color32(255, 210, 117, 255), 18, 252, 520, 30, TextAnchor.MiddleLeft, FontStyle.Bold);

            // 队长特性同样属于未公开内容：这里只给占位说明，不展示真实指挥效果。
            GameObject captainPanel = ModalPolish038Chrome.RowPlate(panel.transform,
                "MemberCaptainPanel");
            PlaceTop(captainPanel.GetComponent<RectTransform>(), 28, 790, 564, 130);
            FlowText(captainPanel.transform, "CaptainEffectTitle", MemberProfileSections.PassiveCaptainTitle,
                16, 16, 10, 532, 26, Cyan);
            FlowText(captainPanel.transform, "CaptainEffectDescription", "签约后解锁该成员的队长特性。",
                14, 16, 43, 320, 72, Muted);
            GameObject appoint = ModalPolish038Chrome.ActionButton(captainPanel.transform,
                "AppointCaptain", "签约后可任命", 15, () => { }, ModalPolish038ButtonStyle.Ghost);
            PlaceTop(appoint.GetComponent<RectTransform>(), 352, 52, 196, 52);
            ModalPolish038Chrome.SetActionEnabled(appoint, false);

            GameObject acquire = ModalPolish038Chrome.ActionButton(panel.transform,
                "AcquireMember", "前往选秀", 18, () =>
                {
                    CloseModal();
                    ShowScreen("audition");
                }, ModalPolish038ButtonStyle.Primary);
            PlaceTop(acquire.GetComponent<RectTransform>(), 154, 950, 312, 60);

        }

        /// <summary>
        /// Real normal-attack copy. idol-v1 members basic-attack with the authored "普攻"
        /// (rt-basic, 100% attack); legacy rows use the shared tactics "strike" definition.
        /// </summary>
        private void MemberNormalAttackCopy(MemberDefinition member, out string name, out string effect)
        {
            if (model.Tactics.FindUnit(member.Id)?.GrowthModel == "idol-v1")
            {
                name = BattleSimulator.BasicAttackName;
                effect = "单体100%攻击伤害，无冷却。";
                return;
            }

            SkillDefinition strike = model.Tactics.FindSkill("strike");
            if (strike != null)
            {
                name = strike.Name;
                effect = MemberBattlePresentation.DescribeSkill(strike);
                return;
            }

            name = "普通攻击";
            effect = "单体100%攻击伤害，无冷却。";
        }
    }
}
