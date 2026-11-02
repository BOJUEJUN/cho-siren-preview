using System;
using System.Collections;
using ChoSiren.Panels;
using ChoSiren.Systems.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 经纪合约 (2026-10-28, 少爷: "得加一个刚刚进游戏要设置的用户名"): the first launch lays a paper contract on the
    /// desk to sign a name and clip an ID photo to; the profile card reopens it as 改名. Signing presses the agency
    /// seal and hands over to the prologue.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        /// <summary>The top-bar / profile portrait for avatar <paramref name="index"/> (0 = the manager).</summary>
        private Sprite PlayerAvatarSprite(int index) => UI.PlayerAvatar1102.Preset(index);

        /// <summary>A name the shared UI font can draw (the WebGL font is a 3,755-hanzi subset).</summary>
        private static bool CanDrawNameCharacter(char c) =>
            c == ' ' || ModalPolish038Chrome.SharedFont == null || ModalPolish038Chrome.SharedFont.HasCharacter(c);

        private void RefreshPlayerIdentity1028()
        {
            if (safeRoot == null || model == null) return;
            Text name = safeRoot.Find("TopBar/Profile/PlayerName")?.GetComponent<Text>();
            if (name != null && name.text != model.PlayerName) { name.text = model.PlayerName; FitPlayerName1028(name); }
            Image avatar = safeRoot.Find("TopBar/Profile/AvatarMask/Avatar")?.GetComponent<Image>();
            Sprite sprite = PlayerAvatarSpriteNow();
            if (avatar != null && sprite != null && avatar.sprite != sprite) avatar.sprite = sprite;
            Transform profile = safeRoot.Find("ProfileModal");
            if (profile != null && sprite != null)
                foreach (Image picture in profile.GetComponentsInChildren<Image>(true))
                    if (picture.name == "Avatar" && picture.transform.parent.name == "AvatarMask")
                        picture.sprite = sprite;
        }

        /// <summary>The header name overflows its band (it must never truncate away), so long names step the size down.</summary>
        private static void FitPlayerName1028(Text name)
        {
            int characters = Mathf.Max(1, name.text.Length);
            name.fontSize = Mathf.Clamp(Mathf.FloorToInt(name.rectTransform.sizeDelta.x / characters), 14, 22);
        }

        /// <param name="firstSigning">The first-launch contract: no close button, 签约 seals it.</param>
        /// <param name="signed">Runs after the seal, once the page has closed.</param>
        private void OpenPlayerContract1028(bool firstSigning, Action signed)
        {
            // 2026-10-28 (少爷: "更像现实…名字是在合同的那个地方…不用太整齐…更像自然的签名"): a paper contract lying a
            // little askew on the desk, an ID photo taped to its corner, and the name signed in ink on the 乙方 line.
            CloseModal();
            Font paperFont = ModalPolish038Chrome.SharedFont;
            Color ink = UI.SignatureInk1028.Ink;
            Color print = new Color32(38, 32, 48, 255);
            Color faint = new Color32(120, 112, 128, 255);
            int avatarChoice = model.PlayerAvatar;

            GameObject page = NewImage("PlayerContract1028", safeRoot, null, new Color32(10, 6, 22, 255));
            Stretch(page.GetComponent<RectTransform>(), 0f, -600f, 0f, 600f); // past the safe area: no lobby peeking
            page.GetComponent<Image>().raycastTarget = true;
            modalObject = page;
            // The P5 desk: one teal slash and a giant ghost word behind the paper.
            GameObject slash = NewImage("Slash", page.transform, PunkUi.SlantFill, Brand1025.TealA(38));
            RectTransform slashRect = slash.GetComponent<RectTransform>();
            slashRect.anchorMin = slashRect.anchorMax = slashRect.pivot = new Vector2(.5f, .5f);
            slashRect.sizeDelta = new Vector2(1100f, 260f);
            slashRect.anchoredPosition = new Vector2(0f, 260f);
            slashRect.localEulerAngles = new Vector3(0f, 0f, 14f);
            Text ghost = NewText("Ghost", page.transform, "CONTRACT", 150, new Color(1f, 1f, 1f, .018f), FontStyle.BoldAndItalic, TextAnchor.MiddleCenter);
            ghost.rectTransform.anchorMin = ghost.rectTransform.anchorMax = new Vector2(.5f, .5f);
            ghost.rectTransform.sizeDelta = new Vector2(1000f, 220f);
            ghost.rectTransform.anchoredPosition = new Vector2(-20f, -470f);
            ghost.rectTransform.localEulerAngles = new Vector3(0f, 0f, -8f);
            ghost.horizontalOverflow = HorizontalWrapMode.Overflow;

            const float paperW = 640f, paperH = 760f;
            RectTransform paper = NewImage("Paper", page.transform, null, new Color32(246, 241, 229, 255)).GetComponent<RectTransform>();
            paper.anchorMin = paper.anchorMax = paper.pivot = new Vector2(.5f, .5f);
            paper.sizeDelta = new Vector2(paperW, paperH);
            paper.anchoredPosition = new Vector2(4f, 90f);
            paper.localEulerAngles = new Vector3(0f, 0f, -2.2f);
            GameObject shade = NewImage("PaperShadow", page.transform, null, new Color(0f, 0f, 0f, .55f));
            RectTransform shadeRect = shade.GetComponent<RectTransform>();
            shadeRect.anchorMin = shadeRect.anchorMax = shadeRect.pivot = new Vector2(.5f, .5f);
            shadeRect.sizeDelta = paper.sizeDelta;
            shadeRect.anchoredPosition = paper.anchoredPosition + new Vector2(12f, -14f);
            shadeRect.localEulerAngles = paper.localEulerAngles;
            shade.transform.SetSiblingIndex(paper.GetSiblingIndex());
            // A faint watermark and the printed form.
            Text mark = NewText("Watermark", paper, "CS", 260, new Color(.2f, .15f, .3f, .05f), FontStyle.BoldAndItalic, TextAnchor.MiddleCenter);
            PlaceTop(mark.rectTransform, 0f, 240f, paperW, 300f);
            Text number = NewText("Number", paper, $"No. CS-{DateTime.Now:yyyyMMdd}", 13, faint, FontStyle.Normal, TextAnchor.MiddleLeft);
            PlaceTop(number.rectTransform, 40f, 26f, 300f, 20f);
            Text title = NewText("Title", paper, "经纪人聘用合约", 40, print, FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceTop(title.rectTransform, 0f, 50f, paperW, 64f);
            title.verticalOverflow = VerticalWrapMode.Overflow; // a 40pt line needs ~58px; Truncate drops it whole
            Text sub = NewText("Subtitle", paper, "幻域魅声事务所  ·  CHO-SIREN", 15, faint, FontStyle.Normal, TextAnchor.MiddleCenter);
            PlaceTop(sub.rectTransform, 0f, 112f, paperW, 22f);
            for (int i = 0; i < 2; i++)
            {
                GameObject rule = NewImage("Rule-" + i, paper, null, print);
                PlaceTop(rule.GetComponent<RectTransform>(), 40f, 146f + i * 5f, paperW - 80f, i == 0 ? 2.2f : 1f);
            }
            Text body = NewText("Terms", paper, string.Empty, 19, print, FontStyle.Normal, TextAnchor.UpperLeft);
            PlaceTop(body.rectTransform, 44f, 172f, paperW - 88f, 300f);
            body.verticalOverflow = VerticalWrapMode.Overflow;
            body.lineSpacing = 1.28f;
            void WriteTerms(string name)
            {
                string party = string.IsNullOrEmpty(name) ? "＿＿＿＿＿＿" : name;
                body.text = "甲方：幻域魅声事务所\n" +
                            $"乙方：{party}（以下简称“经纪人”）\n\n" +
                            "一、乙方自签约之日起，担任女团 CHO-SIREN 的专属经纪人，负责成员的编队、训练与全部舞台演出。\n" +
                            "二、甲方全力支持乙方的工作；乙方承诺带领 CHO-SIREN 驱散「杂音」，让幻域重新亮起来。\n" +
                            "三、本合约自双方签字盖章之日起生效，一式两份。";
            }

            // ID photo taped over the top-right corner; tapping it opens the avatar sheet.
            RectTransform photo = NewImage("IdPhoto", paper, null, Color.white).GetComponent<RectTransform>();
            PlaceTop(photo, paperW - 158f, -26f, 142f, 168f);
            photo.localEulerAngles = new Vector3(0f, 0f, 7f);
            photo.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, .35f);
            GameObject photoMask = NewImage("Clip", photo, null, Color.white);
            PlaceTop(photoMask.GetComponent<RectTransform>(), 9f, 9f, 124f, 124f);
            photoMask.AddComponent<RectMask2D>();
            Image photoFace = NewImage("Face", photoMask.transform, PlayerAvatarSpriteNow(), Color.white).GetComponent<Image>();
            Stretch(photoFace.rectTransform);
            photoFace.preserveAspect = true;
            Text photoNote = NewText("Note", photo, "证件照 · 点我换", 12, faint, FontStyle.Italic, TextAnchor.MiddleCenter);
            PlaceTop(photoNote.rectTransform, 0f, 136f, 142f, 26f);
            GameObject tape = NewImage("Tape", photo, null, Brand1025.TealA(150));
            PlaceTop(tape.GetComponent<RectTransform>(), 30f, -14f, 84f, 26f);
            tape.transform.localEulerAngles = new Vector3(0f, 0f, -11f);
            Button photoButton = photo.gameObject.AddComponent<Button>();
            photoButton.transition = Selectable.Transition.None;
            photo.GetComponent<Image>().raycastTarget = true;

            // Signature blocks: 甲方 (the seal lands here) and 乙方 (the name is signed on the line).
            const float blocksTop = 486f;
            Text partyA = NewText("PartyA", paper, "甲方（盖章）", 17, print, FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceTop(partyA.rectTransform, 44f, blocksTop, 220f, 26f);
            RectTransform sealSpot = NewImage("SealSpot", paper, null, Color.clear).GetComponent<RectTransform>();
            PlaceTop(sealSpot, 60f, blocksTop + 36f, 170f, 170f);
            Text partyB = NewText("PartyB", paper, "乙方（签字）", 17, print, FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceTop(partyB.rectTransform, 300f, blocksTop, 220f, 26f);
            GameObject signLine = NewImage("SignLine", paper, null, print);
            PlaceTop(signLine.GetComponent<RectTransform>(), 300f, blocksTop + 136f, 296f, 1.6f);
            // 2026-10-28 (少爷: "交互要简单…直接弹出输入框…字做手写的特效"): the line is a button that opens one plain
            // name box; once confirmed, the name is signed onto the line stroke by stroke.
            RectTransform signArea = NewImage("SignArea", paper, null, Color.clear).GetComponent<RectTransform>(); // clear still takes taps
            PlaceTop(signArea, 300f, blocksTop + 40f, 296f, 100f);
            signArea.GetComponent<Image>().raycastTarget = true;
            Text placeholder = NewText("Placeholder", signArea, "点这里签名……", 24, new Color(.47f, .44f, .52f, .7f), FontStyle.Italic, TextAnchor.MiddleLeft);
            Stretch(placeholder.rectTransform, 8f, 0f, 0f, -8f);
            var signature = UI.SignatureInk1028.Create(signArea, paperFont, 52);
            Text date = NewText("Date", paper, $"日期：{DateTime.Now:yyyy 年 M 月 d 日}", 16, ink, FontStyle.Italic, TextAnchor.MiddleLeft);
            PlaceTop(date.rectTransform, 304f, blocksTop + 150f, 290f, 26f);
            date.rectTransform.localEulerAngles = new Vector3(0f, 0f, 1.5f);

            Text hint = NewText("Hint", page.transform, string.Empty, 16, ModalPolish038Chrome.TextMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(.5f, .5f);
            hint.rectTransform.sizeDelta = new Vector2(640f, 28f);
            hint.rectTransform.anchoredPosition = new Vector2(0f, paper.anchoredPosition.y - paperH * .5f - 34f);
            string signedName = model.NeedsPlayerName ? string.Empty : model.PlayerName;
            void ShowHint(string problem)
            {
                hint.color = problem == null ? ModalPolish038Chrome.TextMuted : Warn;
                hint.text = problem ?? (signedName.Length == 0 ? "点签名栏，写下你的名字"
                    : firstSigning ? "点「签约」盖章" : "改好后点「确认修改」");
            }
            void SignName(string name, bool animate)
            {
                signedName = name;
                placeholder.gameObject.SetActive(name.Length == 0);
                // The empty signing guide must not show through / extend beyond the ink flourish.
                signLine.SetActive(name.Length == 0);
                if (animate) signature.Write(name); else signature.SetName(name);
                WriteTerms(name);
                ShowHint(null);
            }
            void AskName()
            {
                gameAudio?.PlayClick();
                OpenNameDialog1028(page.transform, signedName, name => SignName(name, true));
            }
            Button signButton = signArea.gameObject.AddComponent<Button>();
            signButton.transition = Selectable.Transition.None;
            signButton.onClick.AddListener(AskName);
            SignName(signedName, false);

            // The seal: red ink, already pressed when renaming, pressed by 签约 on first signing.
            GameObject seal = BuildCompanySeal1028(sealSpot);
            seal.SetActive(!firstSigning);
            photoButton.onClick.AddListener(() =>
            {
                gameAudio?.PlayClick();
                OpenAvatarSheet1028(page.transform, avatarChoice, choice =>
                {
                    avatarChoice = choice;
                    model.SetPlayerAvatarPreset(choice);
                    photoFace.sprite = PlayerAvatarSpriteNow();
                    RefreshPlayerIdentity1028();
                });
            });

            GameObject sign = ModalPolish038Chrome.ActionButton(page.transform, "SignContract", firstSigning ? "签  约" : "确认修改", 26, () =>
            {
                if (signedName.Length == 0 || signature.Writing) { if (signedName.Length == 0) AskName(); return; }
                if (!model.TrySetPlayerProfile(signedName, avatarChoice, CanDrawNameCharacter, out string error))
                {
                    ShowHint(error);
                    gameAudio?.PlayClick();
                    return;
                }
                RefreshPlayerIdentity1028();
                gameAudio?.PlayClick();
                if (!firstSigning)
                {
                    CloseModal();
                    Toast($"已改名为「{model.PlayerName}」");
                    signed?.Invoke();
                    return;
                }
                StartCoroutine(StampContract1028(page, seal, signed));
            });
            RectTransform signRect = sign.GetComponent<RectTransform>();
            signRect.anchorMin = signRect.anchorMax = signRect.pivot = new Vector2(.5f, .5f);
            signRect.sizeDelta = new Vector2(320f, 70f);
            signRect.anchoredPosition = new Vector2(18f, hint.rectTransform.anchoredPosition.y - 66f);
            signRect.localEulerAngles = new Vector3(0f, 0f, 1.5f);

            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            // First signing: the name box pops up by itself, so nobody has to guess where to type.
            // It waits for the native title screen to leave: focusing under it raised the phone keyboard bar over
            // 「点击屏幕」 and swallowed the first tap (2026-10-28).
            if (firstSigning && signedName.Length == 0) StartCoroutine(AfterTitle1028(.45f, () => { if (modalObject == page) AskName(); }));
            if (!firstSigning)
            {
                GameObject close = ModalPolish038Chrome.ActionButton(page.transform, "Close", "×", 28, CloseModal, ModalPolish038ButtonStyle.Ghost);
                RectTransform closeRect = close.GetComponent<RectTransform>();
                closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
                closeRect.sizeDelta = new Vector2(64f, 52f);
                closeRect.anchoredPosition = new Vector2(-20f, -120f);
            }
        }

        private static IEnumerator AfterTitle1028(float seconds, Action then)
        {
            while (FindAnyObjectByType<UI.NativeTitleScreen>() != null) yield return null;
            yield return AfterSeconds1028(seconds, then);
        }

        private static IEnumerator AfterSeconds1028(float seconds, Action then)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until) yield return null;
            then?.Invoke();
        }

        /// <summary>One plain box for the name (with 随机), on the shared modal card; 确定 hands back a valid name.</summary>
        private void OpenNameDialog1028(Transform host, string current, Action<string> confirmed)
        {
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(host, "NameDialog1028", "写下你的名字", "乙方签名",
                null, 600f, closeName: "Close");
            GameObject overlay = frame.Overlay;
            frame.CloseButton = ModalPolish038Chrome.ActionButton(frame.Panel, "Close", "×", 26, () => Destroy(overlay),
                ModalPolish038ButtonStyle.Ghost);
            PlaceTop(frame.CloseButton.GetComponent<RectTransform>(), frame.Width * .895f - 56f, frame.Height * .045f, 56f, 46f);
            RectTransform body = frame.Body;
            float width = frame.Width * (ModalPolish038Chrome.BodyRight - ModalPolish038Chrome.BodyLeft);
            const float boxHeight = 72f, randomWidth = 100f;
            Text intro = ModalPolish038Chrome.Text(body, "Intro", "之后可在档案里改", 16,
                ModalPolish038Chrome.TextMuted, FontStyle.Normal, TextAnchor.MiddleLeft);
            PlaceTop(intro.rectTransform, 0f, 0f, width, 26f);
            GameObject box = ModalPolish038Chrome.RowPlate(body, "NameBox");
            PlaceTop(box.GetComponent<RectTransform>(), 0f, 38f, width - randomWidth - 12f, boxHeight);
            box.GetComponent<Image>().raycastTarget = true;
            Text value = ModalPolish038Chrome.Text(box.transform, "Value", string.Empty, 30,
                ModalPolish038Chrome.TextWhite, FontStyle.Bold, TextAnchor.MiddleLeft);
            Stretch(value.rectTransform, 20f, 4f, -16f, -4f);
            value.verticalOverflow = VerticalWrapMode.Overflow;
            Text placeholder = ModalPolish038Chrome.Text(box.transform, "Placeholder", $"最多 {GameModel.PlayerNameMaxLength} 个字", 22,
                new Color(1f, 1f, 1f, .38f), FontStyle.Italic, TextAnchor.MiddleLeft);
            Stretch(placeholder.rectTransform, 20f, 4f, -16f, -4f);
            InputField input = box.AddComponent<InputField>();
            input.targetGraphic = box.GetComponent<Image>();
            input.textComponent = value;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = GameModel.PlayerNameMaxLength;
            input.caretBlinkRate = .85f;
            input.customCaretColor = true;
            input.caretColor = Brand1025.Teal;
            input.selectionColor = new Color(.1f, .85f, .8f, .35f);
            input.text = current ?? string.Empty;
            Text hint = ModalPolish038Chrome.Text(body, "Hint", string.Empty, 15, ModalPolish038Chrome.TextMuted,
                FontStyle.Normal, TextAnchor.MiddleLeft);
            PlaceTop(hint.rectTransform, 2f, 38f + boxHeight + 8f, width, 24f);
            void ShowHint(string problem)
            {
                hint.color = problem == null ? ModalPolish038Chrome.TextMuted : Warn;
                hint.text = problem ?? $"{GameModel.CleanPlayerName(input.text).Length}/{GameModel.PlayerNameMaxLength}";
            }
            input.onValueChanged.AddListener(text => ShowHint(string.IsNullOrEmpty(text) ? null : GameModel.PlayerNameProblem(text, CanDrawNameCharacter)));
            ShowHint(null);
            GameObject random = ModalPolish038Chrome.ActionButton(body, "RandomName", "随机", 18, () =>
            {
                var options = new System.Collections.Generic.List<string>();
                foreach (string candidate in GameModel.SuggestedPlayerNames)
                    if (candidate != input.text && GameModel.PlayerNameProblem(candidate, CanDrawNameCharacter) == null) options.Add(candidate);
                // Close the phone keyboard first: dismissing it later would restore the text it opened with.
                if (input.isFocused) input.DeactivateInputField();
                if (options.Count > 0) input.text = options[UnityEngine.Random.Range(0, options.Count)];
                gameAudio?.PlayClick();
            }, ModalPolish038ButtonStyle.Secondary);
            PlaceTop(random.GetComponent<RectTransform>(), width - randomWidth, 38f + 8f, randomWidth, boxHeight - 16f);
            GameObject ok = ModalPolish038Chrome.ActionButton(body, "ConfirmName", "确定", 22, () =>
            {
                if (input.isFocused) input.DeactivateInputField();
                string problem = GameModel.PlayerNameProblem(input.text, CanDrawNameCharacter);
                if (problem != null) { ShowHint(problem); gameAudio?.PlayClick(); return; }
                string name = GameModel.CleanPlayerName(input.text);
                gameAudio?.PlayClick();
                Destroy(overlay);
                confirmed?.Invoke(name);
            });
            PlaceTop(ok.GetComponent<RectTransform>(), (width - 260f) * .5f, 38f + boxHeight + 46f, 260f, 62f);
            ModalPolish038Chrome.FitHeight(frame, 38f + boxHeight + 46f + 70f);
            // The narrow desktop window can scale the 720-unit stage down below half size.
            // Resolve layout first, then keep the caret at least two actual screen pixels wide.
            Canvas.ForceUpdateCanvases();
            float scale = value.canvas != null ? value.canvas.scaleFactor : 1f;
            input.caretWidth = Mathf.Max(3, Mathf.CeilToInt(2f / Mathf.Max(.01f, scale)));
            // Straight into typing: the field is focused (the phone keyboard opens on its own).
            input.ActivateInputField();
        }

        /// <summary>The agency's round red seal (幻域魅声事务所), ink a little uneven like a real stamp.</summary>
        private GameObject BuildCompanySeal1028(RectTransform spot)
        {
            Color red = new Color32(200, 24, 46, 225);
            GameObject seal = NewImage("CompanySeal", spot, DotSprite(), red);
            Stretch(seal.GetComponent<RectTransform>());
            seal.transform.localEulerAngles = new Vector3(0f, 0f, -14f);
            GameObject inner = NewImage("Paper", seal.transform, DotSprite(), new Color32(246, 241, 229, 255));
            Stretch(inner.GetComponent<RectTransform>(), 9f, 9f, -9f, -9f);
            GameObject ring = NewImage("Ring", inner.transform, DotSprite(), red);
            Stretch(ring.GetComponent<RectTransform>(), 7f, 7f, -7f, -7f);
            GameObject core = NewImage("Core", ring.transform, DotSprite(), new Color32(246, 241, 229, 255));
            Stretch(core.GetComponent<RectTransform>(), 3f, 3f, -3f, -3f);
            Text star = NewText("Star", core.transform, "★", 34, red, FontStyle.Normal, TextAnchor.MiddleCenter);
            PlaceTop(star.rectTransform, 0f, 28f, 140f, 40f);
            Text name = NewText("Name", core.transform, "幻域魅声\n事务所", 21, red, FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceTop(name.rectTransform, 0f, 64f, 140f, 60f);
            name.lineSpacing = .9f;
            Text use = NewText("Use", core.transform, "合同专用章", 12, red, FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceTop(use.rectTransform, 0f, 118f, 140f, 18f);
            return seal;
        }

        /// <summary>The avatar sheet over the contract: every preset as a large face, tap one to clip it on.</summary>
        private void OpenAvatarSheet1028(Transform host, int current, Action<int> picked)
        {
            GameObject overlay = null;
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(host, "AvatarSheet1028", "选择证件照", "经纪人头像",
                () => { if (overlay != null) Destroy(overlay); }, 640f, closeName: "Close");
            overlay = frame.Overlay;
            float width = frame.Width * (ModalPolish038Chrome.BodyRight - ModalPolish038Chrome.BodyLeft);
            int count = GameModel.PlayerAvatarResources.Length;
            const int columns = 4;
            const float rowGap = 16f;
            float face = (width - (columns - 1) * 16f) / columns;
            int rows = Mathf.Max(1, (count + columns - 1) / columns);
            RectTransform content = AvatarScrollContent1102(frame, width,
                rows * face + (rows - 1) * rowGap + 8f, out Func<bool> canPick);
            for (int i = 0; i < count; i++)
            {
                int choice = i;
                bool on = i == current;
                GameObject ring = NewImage("Avatar-" + i, content, DotSprite(), on ? (Color)Brand1025.Teal : new Color(1f, 1f, 1f, .16f));
                PlaceTop(ring.GetComponent<RectTransform>(), i % columns * (face + 16f), i / columns * (face + rowGap), face, face);
                ring.GetComponent<Image>().raycastTarget = true;
                GameObject mask = NewImage("Mask", ring.transform, DotSprite(), Color.white);
                Stretch(mask.GetComponent<RectTransform>(), 6f, 6f, -6f, -6f);
                mask.AddComponent<Mask>().showMaskGraphic = false;
                Sprite portrait = PlayerAvatarSprite(i);
                if (portrait != null)
                {
                    Image picture = NewImage("Face", mask.transform, portrait, Color.white).GetComponent<Image>();
                    Stretch(picture.rectTransform);
                    picture.preserveAspect = true;
                }
                Button pick = ring.AddComponent<Button>();
                pick.transition = Selectable.Transition.None;
                pick.onClick.AddListener(() =>
                {
                    if (!canPick()) return;
                    gameAudio?.PlayClick();
                    picked?.Invoke(choice);
                    Destroy(overlay);
                });
            }
        }

        private static IEnumerator NextFrame1028(Action then)
        {
            yield return null;
            then?.Invoke();
        }

        /// <summary>The lobby's own greeting: the first-visit guide, or on later days the sign-in card.</summary>
        private void GreetLobby1028()
        {
            // First lobby visit: a short walkthrough that ends at the first performance.
            bool guide = !Application.isBatchMode && model.FirstTime("guide-lobby-v1");
            // Later days: the sign-in card greets the first lobby visit of the day.
            if (!guide && !Application.isBatchMode && !model.SignedToday &&
                model.FirstTime("signin-popup:" + PeriodKeys.Daily(DateTime.Now)))
                OpenSignIn();
            // 2026-10-28: one spotlight on 「开始演出」 instead of the old wall-of-text welcome card (one line since 11-01).
            if (guide) StartCoroutine(LobbyCoach1028());
        }

        /// <summary>序章 · 霓虹熄灭之夜: plays once (finished or skipped), then <paramref name="then"/>.</summary>
        private void PlayPrologue1028(Action then)
        {
            if (model.IsStoryCompleted(GameModel.PrologueStoryId) ||
                !model.TryStartStory(GameModel.PrologueStoryId, out ChoSiren.Systems.Story.StoryRunner runner, out _))
            {
                then?.Invoke();
                return;
            }
            void Done()
            {
                model.CompleteStory(GameModel.PrologueStoryId);
                UpdateTopBar();
                then?.Invoke();
            }
            StoryPanel.Open(safeRoot, model, runner, finished: Done, back: Done, message: Toast);
        }

        /// <summary>The agency seal thuds onto 甲方, the pen rests a beat, then the contract slides off the desk.</summary>
        private IEnumerator StampContract1028(GameObject page, GameObject seal, Action signed)
        {
            foreach (Selectable control in page.GetComponentsInChildren<Selectable>()) control.interactable = false;
            seal.SetActive(true);
            RectTransform rect = (RectTransform)seal.transform;
            CanvasGroup group = seal.AddComponent<CanvasGroup>();
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / .16f)
            {
                float e = 1f - (1f - t) * (1f - t);
                rect.localScale = Vector3.one * Mathf.Lerp(2.1f, 1f, e);
                group.alpha = Mathf.Clamp01(t * 2.4f);
                yield return null;
            }
            rect.localScale = Vector3.one;
            group.alpha = 1f;
            ModalPolish038Chrome.PlayClick();
            float hold = Time.unscaledTime + .8f;
            while (Time.unscaledTime < hold) yield return null;
            Transform paper = page.transform.Find("Paper");
            Transform shadow = page.transform.Find("PaperShadow");
            if (paper != null)
            {
                Vector2 from = ((RectTransform)paper).anchoredPosition;
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / .32f)
                {
                    float e = t * t;
                    Vector2 at = from + new Vector2(e * 260f, e * 1400f);
                    ((RectTransform)paper).anchoredPosition = at;
                    paper.localEulerAngles = new Vector3(0f, 0f, -2.2f - e * 12f);
                    if (shadow != null) { ((RectTransform)shadow).anchoredPosition = at + new Vector2(12f, -14f); shadow.localEulerAngles = paper.localEulerAngles; }
                    yield return null;
                }
            }
            if (modalObject == page) CloseModal();
            else if (page != null) Destroy(page);
            Toast($"欢迎你，{model.PlayerName}");
            signed?.Invoke();
        }
    }
}
