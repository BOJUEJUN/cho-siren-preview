using System;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 2026-11-07 团队 (赵金 团队.psd): preset chips top-left, the 队伍 plate (tap to rename), the black 队伍战力 banner
    /// with 成员 n/4, four torn purple cards (bust, race icon + role, LV, race · role, 战力, 换人 ›, 队长 tag on the
    /// captain), and the 更换队长 / 一键编队 stickers. The board art has its dynamic text painted out; live text and
    /// hit areas keep the names the rest of the game and the tests use (TeamOrbit-n, TeamLabel-n, TeamPowerValue …).
    /// PSD (1994 wide) → board: 0.39 scale, 43 left, PSD y 420 at board y 195.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        private const string TeamArt1107 = "Art/Ui1107/";
        private static float TX(float x) => 43f + x * .39f;
        private static float TY(float y) => 195f + (y - 420f) * .39f;
        private static Rect Team1107(float x0, float y0, float x1, float y1) => new Rect(TX(x0), TY(y0), (x1 - x0) * .39f, (y1 - y0) * .39f);

        private static readonly Rect[] TeamCardFrame1107 =
            { new Rect(100, 1350, 900, 2470), new Rect(950, 1380, 1750, 2450), new Rect(110, 2500, 980, 3500), new Rect(1080, 2550, 1900, 3480) };
        private static readonly Rect[] TeamCardWindow1107 =
            { new Rect(190, 1440, 860, 2190), new Rect(1015, 1480, 1655, 2210), new Rect(185, 2600, 925, 3270), new Rect(1115, 2600, 1860, 3250) };
        private static readonly Vector2[] TeamCardIcon1107 = { new Vector2(195, 1395), new Vector2(1020, 1480), new Vector2(155, 2595), new Vector2(1105, 2570) };
        private static readonly Vector2[] TeamCardLevel1107 = { new Vector2(700, 2075), new Vector2(1565, 2050), new Vector2(808, 3040), new Vector2(1720, 3062) };
        private static readonly Vector2[] TeamCardBand1107 = { new Vector2(130, 2240), new Vector2(995, 2265), new Vector2(205, 3265), new Vector2(1095, 3257) };
        private static readonly Vector2[] TeamCardCaptain1107 = { new Vector2(330, 1330), new Vector2(1180, 1362), new Vector2(426, 2469), new Vector2(1250, 2490) };

        private static Rect XY(Rect psd) => Team1107(psd.x, psd.y, psd.width, psd.height); // Rect stores x0,y0,x1,y1 here

        private void BuildTeamBoard1107()
        {
            RectTransform root = ReferenceBoard038("TeamReferenceBoard038", TeamArt1107 + "page-bg-board");
            CoverPageBackground1108(root);
            int count = Math.Min(GameModel.TeamCapacity, model.Save.Team.Count);
            int roles = model.Save.Team.Take(count).Where(i => i >= 0 && i < GameModel.Members.Length)
                .Select(i => GameModel.Members[i].Career).Distinct().Count();

            // 2026-11-08 (少爷: "猪也是很生硬的放上去"): as in 团队.psd the pig sits under 主UI and peeks over the banner's top edge.
            Image pig = NewImage("TeamPig", root, Resources.Load<Sprite>(TeamArt1107 + "team-pig"), White).GetComponent<Image>();
            Rect pigRect = Team1107(780, 312, 1217, 632);
            PlaceTop(pig.rectTransform, pigRect.x, pigRect.y, pigRect.width, pigRect.height);
            pig.raycastTarget = false;
            Image board = NewImage("TeamBoard1107", root, Resources.Load<Sprite>(TeamArt1107 + "team-board"), White).GetComponent<Image>();
            Rect boardRect = Team1107(30, 420, 1994, 4272);
            PlaceTop(board.rectTransform, boardRect.x, boardRect.y, boardRect.width, boardRect.height);
            board.raycastTarget = false;

            BuildTeamPresetControls1028(root, TX(40), TY(458), true, TX(770) - TX(40));

            // 队伍 plate: the name (tap to rename) and the pencil.
            Rect plate = Team1107(325, 950, 675, 1100);
            GameObject titleHit = BoardButton038("RenameTeamTitle", root, plate, () => OpenTeamNameDialog1028(model.Save.ActiveTeamPreset));
            titleHit.GetComponent<Image>().color = Color.clear;
            Text teamTitle = BoardText038(root, "TeamTitle", model.ActiveTeamName, 40, White, plate.x, plate.y, plate.width, plate.height, TextAnchor.MiddleCenter);
            teamTitle.fontStyle = FontStyle.BoldAndItalic;
            teamTitle.raycastTarget = false;
            PanelKit.EnableBestFit(teamTitle, 20);
            TiltText1108(teamTitle, 5f);
            Rect pencil = Team1107(700, 960, 800, 1060);
            GameObject renameTeam = BoardButton038("RenameTeam", root, pencil, () => OpenTeamNameDialog1028(model.Save.ActiveTeamPreset));
            Image renameHit = renameTeam.GetComponent<Image>();
            renameHit.sprite = VisualA1101.Glow;
            renameHit.color = VisualA1101.Teal * new Color(1f, 1f, 1f, .22f);
            RectTransform renamePencil = VisualA1101.NeonIcon(renameTeam.transform, "Pencil", "pencil", VisualA1101.Teal, 30f);
            VisualA1101.Center(renamePencil, 30f);
            if (!model.IsTeamReady(out string formationProblem))
                BoardText038(root, "TeamFormationIssue", formationProblem.Split('\n').Last(), 20, Warn, TX(140), TY(1130), 330, 60);
            BoardText038(root, "TeamResonanceValue", $"职业种类 {roles}/4", 16, Cyan, TX(1380), TY(1280), 220, 24, TextAnchor.MiddleRight);

            // 2026-11-08 (少爷: "战斗力太粗糙…字体是斜的"): the board's number is inpainted out of 主UI (no boxes), and the live
            // number is drawn like the PSD's: huge, italic, white, riding the banner's ~5.5° rise.
            Rect power = Team1107(1030, 660, 1985, 1040);
            Text powerValue = BoardText038(root, "TeamPowerValue", model.TeamPower.ToString("N0"), 160, White, power.x, power.y, power.width, power.height, TextAnchor.MiddleCenter);
            powerValue.fontStyle = FontStyle.BoldAndItalic;
            powerValue.raycastTarget = false;
            PanelKit.EnableBestFit(powerValue, 60);
            powerValue.resizeTextMaxSize = 160;
            TiltText1108(powerValue, 5.5f);
            Rect members = Team1107(1690, 1120, 1960, 1270);
            Text memberCount = BoardText038(root, "TeamMemberCount", $"{count}/{GameModel.TeamCapacity}", 56, new Color32(132, 92, 255, 255),
                members.x, members.y, members.width, members.height, TextAnchor.MiddleLeft);
            memberCount.fontStyle = FontStyle.BoldAndItalic;
            memberCount.raycastTarget = false;
            TiltText1108(memberCount, 4f);

            for (int slot = 0; slot < GameModel.TeamCapacity; slot++) TeamCard1107(root, slot, count);

            TeamSticker1107("ChangeLeader", root, Team1107(375, 3500, 900, 3850), OpenCaptainPicker);
            TeamSticker1107("AutoTeam", root, Team1107(987, 3512, 1575, 3862), () =>
            {
                model.TryAutoTeam(out string message);
                ShowScreen("team");
                Toast(message);
            });
        }

        private void TeamCard1107(RectTransform root, int slot, int count)
        {
            int index = slot < count ? model.Save.Team[slot] : -1;
            bool valid = index >= 0 && index < GameModel.Members.Length;
            Rect frame = XY(TeamCardFrame1107[slot]);
            GameObject hit = null;
            hit = BoardButton038("TeamOrbit-" + slot, root, frame, () =>
            {
                if (!valid) { OpenTeamSlotPicker(Math.Min(slot, model.Save.Team.Count)); return; }
                // 2026-11-08 (赵金 转场): her race's cyan-silhouette card plays before the detail page opens.
                PlayRaceTransition1108(index, hit != null ? (RectTransform)hit.transform : null, () => OpenTeamMember(index, slot));
            });
            hit.GetComponent<Image>().color = Color.clear;
            Rect window = XY(TeamCardWindow1107[slot]);
            Rect local = new Rect(window.x - frame.x, window.y - frame.y, window.width, window.height);
            if (!valid)
            {
                BoardText038(hit.transform, "TeamEmpty-" + slot, "+", 72, Cyan, local.x, local.y + local.height * .3f, local.width, 90, TextAnchor.MiddleCenter);
                BoardText038(hit.transform, "EmptyHint", "添加成员", 20, Muted, local.x, local.y + local.height * .3f + 92f, local.width, 30, TextAnchor.MiddleCenter);
                return;
            }
            MemberDefinition m = GameModel.Members[index];
            string race = MemberRaceFamily(m, index);
            GameObject windowObject = NewImage("PortraitWindow", hit.transform, null, new Color(0, 0, 0, 0));
            PlaceTop(windowObject.GetComponent<RectTransform>(), local.x, local.y, local.width, local.height);
            windowObject.GetComponent<Image>().raycastTarget = false;
            windowObject.AddComponent<RectMask2D>();
            GameObject portrait = NewImage("TeamCharacter-" + slot, windowObject.transform, Resources.Load<Sprite>(m.ResourcePath), White);
            portrait.GetComponent<Image>().preserveAspect = true;
            portrait.GetComponent<Image>().raycastTarget = false;
            PanelKit.FrameBustPortrait(portrait.GetComponent<Image>(), windowObject.GetComponent<RectTransform>(), m.Id);
            GameObject fade = NewImage("Fade", windowObject.transform, BottomFade(), new Color(0, 0, 0, .7f));
            PlaceTop(fade.GetComponent<RectTransform>(), 0, local.height - 70, local.width, 70);
            fade.GetComponent<Image>().raycastTarget = false;

            Vector2 icon = TeamCardIcon1107[slot];
            Sprite raceIcon = RaceIconSprite(race);
            GameObject raceChip = NewImage("RaceChip", hit.transform, raceIcon ?? PunkUi.SlantFill, raceIcon != null ? (Color)White : TeamRaceColor(race));
            PlaceTop(raceChip.GetComponent<RectTransform>(), TX(icon.x) - frame.x + 4f, TY(icon.y) - frame.y, 46, 46);
            raceChip.GetComponent<Image>().preserveAspect = true;
            raceChip.GetComponent<Image>().raycastTarget = false;
            Text role = BoardText038(hit.transform, "Role", m.Career, 15, White, TX(icon.x) - frame.x - 6f, TY(icon.y) - frame.y + 46f, 70, 22, TextAnchor.MiddleCenter);
            role.fontStyle = FontStyle.Bold;
            role.gameObject.AddComponent<Outline>().effectColor = new Color32(10, 6, 24, 255);

            Vector2 lv = TeamCardLevel1107[slot];
            BoardText038(hit.transform, "LvTag", "LV.", 15, White, TX(lv.x) - frame.x, TY(lv.y) - frame.y, 50, 22).fontStyle = FontStyle.Bold;
            Text level = BoardText038(hit.transform, "Level", model.LevelOf(index).ToString(), 44, White, TX(lv.x) - frame.x - 30f, TY(lv.y) - frame.y + 16f, 84, 52, TextAnchor.MiddleRight);
            level.fontStyle = FontStyle.BoldAndItalic;
            Text name = BoardText038(hit.transform, "MemberName", m.Name, 24, White, local.x + 8f, local.y + local.height - 40f, local.width - 100f, 34, TextAnchor.MiddleLeft);
            name.fontStyle = FontStyle.Bold;
            PanelKit.EnableBestFit(name, 16);

            if (slot == 0)
            {
                Vector2 c = TeamCardCaptain1107[slot];
                Image captain = NewImage("CaptainTag", hit.transform, Resources.Load<Sprite>(TeamArt1107 + "team-captain"), White).GetComponent<Image>();
                PlaceTop(captain.rectTransform, TX(c.x) - frame.x, TY(c.y) - frame.y, 529f * .39f, 203f * .39f);
                captain.raycastTarget = false;
            }

            // The band (race · role, 战力, the baked 换人 ›) is its own replacement target.
            Vector2 band = TeamCardBand1107[slot];
            float bandY = TY(band.y) - frame.y - 4f;
            GameObject tag = BoardButton038("TeamLabel-" + slot, hit.transform, new Rect(0, bandY, frame.width, frame.height - bandY), () => OpenTeamSlotPicker(slot));
            tag.GetComponent<Image>().color = Color.clear;
            BoardText038(tag.transform, "MemberCareer", $"{race} · {m.Career}", 16, SirenStickerUi.Paper, TX(band.x) - frame.x, 4f, 200, 24).fontStyle = FontStyle.Bold;
            Text powerText = BoardText038(tag.transform, "MemberPower", $"战力 {model.PowerOf(index):N0}", 22, Cyan, TX(band.x) - frame.x, 28f, 160, 32);
            powerText.fontStyle = FontStyle.BoldAndItalic;
            TeamStaminaRow1107(tag.transform, index, TX(band.x) - frame.x, 60f);
        }

        /// <summary>2026-11-07 角色体力 on the card: a slim bar + 体力 n/120, amber when 疲劳, red when she needs rest; tap to rest.</summary>
        private void TeamStaminaRow1107(Transform parent, int member, float x, float y)
        {
            int stamina = model.MemberStamina1107(member);
            bool needsRest = stamina < GameModel.BattleMemberStamina1107;
            bool tired = model.IsTired1107(member);
            Color tone = needsRest ? new Color32(255, 84, 104, 255) : tired ? new Color32(255, 186, 72, 255) : new Color32(120, 236, 160, 255);
            GameObject row = BoardButton038("TeamStamina-" + member, parent, new Rect(x - 4f, y - 2f, 170f, 26f), () => OpenRestDialog1107(member));
            row.GetComponent<Image>().color = Color.clear;
            Image track = NewImage("Track", row.transform, null, new Color(1f, 1f, 1f, .16f)).GetComponent<Image>();
            PlaceTop(track.rectTransform, 4f, 9f, 64f, 8f);
            track.raycastTarget = false;
            Image fill = NewImage("Fill", track.transform, null, tone).GetComponent<Image>();
            Stretch(fill.rectTransform);
            fill.rectTransform.anchorMax = new Vector2(stamina / (float)GameModel.MemberStaminaMax1107, 1f);
            fill.raycastTarget = false;
            string label = needsRest ? "需休息" : tired ? $"疲劳 {stamina}" : $"体力 {stamina}";
            Text text = BoardText038(row.transform, "Label", label, 17, tone, 74f, -1f, 100f, 26f);
            text.fontStyle = FontStyle.Bold;
            text.raycastTarget = false;
        }

        /// <summary>休息: shows the member's stamina, when she is rested, and 补满 for 星光币.</summary>
        /// <summary>Silhouette transition (RaceTransition1108) from a tapped member card, then <paramref name="open"/>.</summary>
        private void PlayRaceTransition1108(int memberIndex, RectTransform from, Action open)
        {
            string race = memberIndex >= 0 && memberIndex < GameModel.Members.Length ? GameModel.Members[memberIndex].Race : null;
            var host = (RectTransform)safeRoot;
            Vector2 tap = Vector2.zero;
            if (from != null && host != null)
            {
                Vector3 world = from.TransformPoint(from.rect.center);
                tap = (Vector2)host.InverseTransformPoint(world) - host.rect.center;
            }
            RaceTransition1108.Play(host, race, tap, open);
        }

        private void OpenRestDialog1107(int member)
        {
            if (member < 0 || member >= GameModel.Members.Length) return;
            int stamina = model.MemberStamina1107(member);
            int seconds = model.SecondsToRested1107(member);
            string when = seconds <= 0 ? "体力已满" : $"再休息 {seconds / 3600} 小时 {seconds / 60 % 60} 分就满";
            var frame = ModalPolish038Chrome.Begin(safeRoot, "MemberRestDialog", $"{GameModel.Members[member].Name} 体力 {stamina}/{GameModel.MemberStaminaMax1107}",
                null, null, 540f);
            float width = frame.Width * (ModalPolish038Chrome.BodyRight - ModalPolish038Chrome.BodyLeft);
            Text info = ModalPolish038Chrome.Text(frame.Body, "RestInfo",
                $"{when}\n每场战斗 -{GameModel.BattleMemberStamina1107}（首领关 -{GameModel.BossMemberStamina1107}），低于 {GameModel.TiredBelow1107} 会疲劳：生命、攻击 -10%",
                22, White, FontStyle.Normal, TextAnchor.UpperLeft);
            info.lineSpacing = 1.3f;
            PlaceTop(info.rectTransform, 0, 0, width, 104);
            int cost = model.RestCost1107(member);
            var restNow = ModalPolish038Chrome.ActionButton(frame.Body, "RestNow", cost > 0 ? $"补满 · {cost:N0} 星光币" : "体力已满", 24, () =>
            {
                bool ok = model.TryRestNow1107(member, out string message);
                Destroy(frame.Overlay);
                Toast(message);
                if (ok) ShowScreen("team");
            }, ModalPolish038ButtonStyle.Primary);
            PlaceTop(restNow.GetComponent<RectTransform>(), 0, 118, width, 72);
            restNow.GetComponent<Button>().interactable = cost > 0;
            var later = ModalPolish038Chrome.ActionButton(frame.Body, "RestLater", "让她休息", 22, () => Destroy(frame.Overlay), ModalPolish038ButtonStyle.Ghost);
            PlaceTop(later.GetComponent<RectTransform>(), 0, 204, width, 64);
            ModalPolish038Chrome.FitHeight(frame, 280f);
        }

        /// <summary>A baked PSD sticker button: an invisible hit area with the lobby's hover / press feedback.</summary>
        private void TeamSticker1107(string name, Transform parent, Rect bounds, UnityEngine.Events.UnityAction action)
        {
            GameObject button = BoardButton038(name, parent, bounds, action);
            button.GetComponent<Image>().color = Color.clear;
            // 2026-11-08 (少爷: "更换队长跟一键编队没有点击感反馈"): the baked sticker itself lifts on hover and presses in.
            Image board = parent.Find("TeamBoard1107")?.GetComponent<Image>();
            if (board != null)
            {
                // Board px: PSD minus (30, 420), scaled to the imported texture (the 1964 px art imports at max 2048 tall).
                float k = board.sprite != null ? board.sprite.rect.width / 1964f / .39f : 1f / .39f;
                Rect region = new Rect((bounds.x - TX(30)) * k, (bounds.y - TY(420)) * k, bounds.width * k, bounds.height * k);
                MainBakedMotion038Attach(button, board, region);
            }
        }

        /// <summary>Rotates a placed label about its centre so it rides a slanted PSD plate.</summary>
        private static void TiltText1108(Text text, float degrees)
        {
            RectTransform r = text.rectTransform;
            Vector2 size = r.sizeDelta;
            r.anchoredPosition += Vector2.Scale(new Vector2(.5f, .5f) - r.pivot, size);
            r.pivot = new Vector2(.5f, .5f);
            r.localEulerAngles = new Vector3(0f, 0f, degrees);
        }
    }
}
