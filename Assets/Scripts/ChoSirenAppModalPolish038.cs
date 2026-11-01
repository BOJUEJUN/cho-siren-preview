using System;
using ChoSiren.Panels;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 0.3.8 secondary-UI polish: settings, generic info modals (producer profile, inbox,
    /// stage income, reset confirmation) and the deep-talk result rebuilt on the approved
    /// illustrated crystal backdrop instead of the rejected giant rounded purple panels.
    /// All object names used by tests are preserved (SettingsModal / InfoModal / DeepTalkModal,
    /// Setting-*, Toggle, Reset, Done, Primary, Close, DeepTalkDialogue,
    /// DeepTalkAffectionResult, BackToProfile) and every action/confirmation keeps its exact
    /// behavior — only the presentation changes. ChoSirenApp.cs delegates here; see
    /// /tmp/cho-parallel-modals-integration.patch.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        // visual-a 1101c: icons + sliders + picture tiles, see ChoSirenApp.VisualA1101.cs.
        private void OpenSettings038() => OpenSettings1101();

        private void ExportSave1021()
        {
            string code = model.ExportSaveCode();
            bool copied = SaveTransferBridge1021.Copy(code);
            bool downloaded = SaveTransferBridge1021.Download("cho-siren-save.txt", code);
            bool ok = copied || downloaded;
            if (ok) UI.VisualA1101.Success(); else UI.VisualA1101.Deny();
            OpenConfirm1101(new Confirm1101
            {
                Title = "导出存档",
                Icon = ok ? "export" : "close",
                Accent = ok ? UI.VisualA1101.Teal : UI.VisualA1101.Danger,
                Note = copied && downloaded ? "已复制 · 已下载" : copied ? "已复制" : downloaded ? "已下载" : "复制失败",
                PrimaryLabel = "返回",
                PrimaryIcon = "check",
                Cancel = OpenSettings038,
                Help = new[]
                {
                    ("device", "换设备或浏览器：设置 → 导入，粘贴存档码即可恢复进度"),
                    ("lock", "存档码包含全部进度，请妥善保存"),
                },
            });
        }

        private void ImportSave1021()
        {
            string code = SaveTransferBridge1021.Paste("粘贴 CHO-SIREN 存档码（CHOSIREN1: 开头）");
            if (string.IsNullOrWhiteSpace(code))
            {
                ImportProblem1101("未收到存档码", SaveTransferBridge1021.CanDownload
                    ? "点「导入」后，在弹出的输入框里粘贴存档码"
                    : "先复制存档码（CHOSIREN1: 开头），再点「导入」");
                return;
            }
            if (!GameModel.TryDecodeSaveCode(code, out GameSave incoming, out _, out string problem))
            {
                ImportProblem1101("存档码无效", problem);
                return;
            }
            int cleared = incoming.ClearedStages != null ? incoming.ClearedStages.Count : 0;
            OpenConfirm1101(new Confirm1101
            {
                Title = "导入存档",
                Icon = "import",
                Accent = UI.VisualA1101.Gold,
                Chips = new[]
                {
                    ("flag", cleared.ToString(), UI.VisualA1101.Paper),
                    (Systems.Economy.CurrencyIds.Diamond, incoming.Diamonds.ToString("N0"), UI.VisualA1101.Paper),
                    (Systems.Economy.CurrencyIds.Gold, incoming.Gold.ToString("N0"), UI.VisualA1101.Paper),
                },
                Note = "覆盖本机",
                PrimaryLabel = "导入",
                PrimaryIcon = "import",
                Primary = () =>
                {
                    if (model.TryImportSaveCode(code, out string message))
                    {
                        UI.VisualA1101.Success();
                        ApplyMusicRouting();
                        ShowScreen("lobby");
                    }
                    else UI.VisualA1101.Deny();
                    Toast(message);
                },
                Cancel = OpenSettings038,
                Help = new[] { ("refresh", "导入会覆盖本机当前存档，无法撤销"), ("close", "关闭此窗口即可取消") },
            });
        }

        private void ImportProblem1101(string note, string detail)
        {
            UI.VisualA1101.Deny();
            OpenConfirm1101(new Confirm1101
            {
                Title = "导入存档",
                Icon = "import",
                Accent = UI.VisualA1101.Danger,
                Note = note,
                PrimaryLabel = "返回",
                PrimaryIcon = "check",
                Cancel = OpenSettings038,
                Help = new[] { ("info", detail) },
            });
        }

        private void OpenSettingsDocument038(int index)
        {
            if (index == 2)
            {
                OpenAbout1101();
                return;
            }
            // 协议 / 隐私 are legal text and stay readable as text.
            string[] titles = { "用户协议（试玩版）", "隐私政策（试玩版）" };
            string[] bodies =
            {
                "本游戏为试玩版本，供个人娱乐体验。\n\n· 游戏内容、数值与美术会随版本更新调整。\n· 存档保存在你的设备本机，清除浏览器数据会删除存档。\n· 部分美术与视频素材由 AI 辅助生成，视频中的「AI 生成」标识会保留。\n· 请合理安排游戏时间。",
                "试玩版不要求注册账号，也不收集你的姓名、手机号等个人信息。\n\n· 游戏进度只保存在本机浏览器里。\n· 游戏不接入广告和第三方统计。\n· 网页由 GitHub Pages 托管，访问记录按其隐私政策处理。",
            };
            OpenInfoModal038(titles[index], bodies[index], "返回", OpenSettings038);
        }

        /// <summary>关于: the logo, the version number and the repair tip behind 「?」.</summary>
        private void OpenAbout1101()
        {
            CloseModal();
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(safeRoot, "InfoModal", "关于", null,
                CloseModal, 520f, closeName: "Close");
            modalObject = frame.Overlay;
            RectTransform body = frame.Body;
            float width = frame.Width * (ModalPolish038Chrome.BodyRight - ModalPolish038Chrome.BodyLeft);
            GameObject help = UI.VisualA1101.HelpButton(frame.Panel, "AboutHelp", frame.Overlay.transform, 44f,
                ("mic", "异界少女化身偶像，以歌声为刃、以舞台为阵"),
                ("wrench", "画面异常时，可在加载页右上角点「修复」重新下载资源，存档不受影响"));
            RectTransform helpRect = help.GetComponent<RectTransform>();
            helpRect.anchorMin = helpRect.anchorMax = helpRect.pivot = new Vector2(1f, 1f);
            helpRect.anchoredPosition = new Vector2(-frame.Width * (1f - ModalPolish038Chrome.BodyRight) - 64f, -frame.Height * .045f - 2f);
            Texture2D logo = Resources.Load<Texture2D>("Art/Loading/loading-title");
            if (logo != null)
            {
                RawImage picture = NewObject("AboutLogo", body).AddComponent<RawImage>();
                picture.texture = logo;
                picture.raycastTarget = false;
                float h = Mathf.Min(150f, width * logo.height / logo.width);
                PlaceTop(picture.rectTransform, (width - h * logo.width / logo.height) * .5f, 0f, h * logo.width / logo.height, h);
            }
            Text version = UI.VisualA1101.Number(body, "AboutVersion", "v" + Application.version, 24, UI.VisualA1101.Teal);
            UI.VisualA1101.Place(version.rectTransform, 0f, 160f, width, 34f);
            GameObject back = ModalPolish038Chrome.ActionButton(body, "Primary", "返回", 20, OpenSettings038, ModalPolish038ButtonStyle.Primary);
            PanelKit.PlaceTop(back.GetComponent<RectTransform>(), (width - 220f) * .5f, 214f, 220f, 56f);
            UI.VisualA1101.DecorateAction(back, "check", 26f);
            ModalPolish038Chrome.FitHeight(frame, 274f);
        }

        private RectTransform SliderArea038(string name, Transform parent)
        {
            GameObject go = NewObject(name, parent);
            RectTransform rect = go.AddComponent<RectTransform>();
            Stretch(rect);
            return rect;
        }

        private void OpenInfoModal038(string title, string body, string primaryLabel,
            UnityEngine.Events.UnityAction primaryAction)
        {
            CloseModal();
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(safeRoot, "InfoModal",
                title, "提示", CloseModal, 520f, closeName: "Close");
            modalObject = frame.Overlay;
            RectTransform content = frame.Body;
            float width = frame.Width * (ModalPolish038Chrome.BodyRight - ModalPolish038Chrome.BodyLeft);
            float height = frame.Height * (ModalPolish038Chrome.BodyBottom - ModalPolish038Chrome.BodyTop);

            Text bodyText = ModalPolish038Chrome.Text(content, "InfoBody", body, 22,
                ModalPolish038Chrome.TextMuted, FontStyle.Normal, TextAnchor.UpperLeft);
            bodyText.lineSpacing = 1.22f;
            // Size the card to the text: measure it, then place the divider and button under it.
            float preferredHeight = bodyText.cachedTextGeneratorForLayout.GetPreferredHeight(body,
                bodyText.GetGenerationSettings(new Vector2(width - 8f, 0f))) / bodyText.pixelsPerUnit + 8f;
            float textHeight = Mathf.Min(height - 150f, preferredHeight);
            GameObject reading = NewImage("InfoScroll", content, null, Color.clear);
            PanelKit.PlaceTop(reading.GetComponent<RectTransform>(), 4f, 8f, width - 8f, textHeight);
            GameObject viewport = NewImage("InfoViewport", reading.transform, null, new Color(0f, 0f, 0f, .001f));
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.AddComponent<RectMask2D>();
            bodyText.transform.SetParent(viewport.transform, false);
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            PanelKit.PlaceTop(bodyText.rectTransform, 0f, 0f, width - 8f, preferredHeight);
            var scroll = reading.AddComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = bodyText.rectTransform;
            scroll.horizontal = false;
            scroll.vertical = preferredHeight > textHeight + 1f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            float y = 8f + textHeight + 18f;
            ModalPolish038Chrome.Divider(content, "InfoDivider", 8f, y, width - 16f);
            UnityEngine.Events.UnityAction confirmed = () =>
            {
                CloseModal();
                primaryAction?.Invoke();
            };
            GameObject primary = ModalPolish038Chrome.ActionButton(content, "Primary",
                primaryLabel, 24, confirmed, ModalPolish038ButtonStyle.Primary);
            PanelKit.PlaceTop(primary.GetComponent<RectTransform>(), (width - 280f) * .5f,
                y + 22f, 280f, 58f);
            ModalPolish038Chrome.FitHeight(frame, y + 22f + 58f + 10f);
        }

        /// <summary>v0.3.4 深入交流结果：立绘 + 羁绊文案 + 好感变化，返回原档案。</summary>
        private void OpenDeepTalkResult038(int memberIndex, int teamSlot, string dialogue,
            int affectionBefore, int affectionAfter)
        {
            MemberDefinition member = GameModel.Members[memberIndex];
            CloseModal();
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(safeRoot, "DeepTalkModal",
                "深入交流", "羁绊时刻", CloseModal, 560f, closeName: "Close");
            modalObject = frame.Overlay;
            RectTransform body = frame.Body;
            float width = frame.Width * (ModalPolish038Chrome.BodyRight - ModalPolish038Chrome.BodyLeft);
            float height = frame.Height * (ModalPolish038Chrome.BodyBottom - ModalPolish038Chrome.BodyTop);

            ModalPolish038Chrome.PortraitSlot(body, "PortraitFrame",
                Resources.Load<Sprite>(member.ResourcePath), member.Id, 0f, 4f, 148f, 196f, White);

            Text name = ModalPolish038Chrome.Text(body, "DeepTalkName", member.Name, 27,
                ModalPolish038Chrome.TextWhite, FontStyle.Bold, TextAnchor.MiddleLeft);
            PanelKit.PlaceTop(name.rectTransform, 168f, 8f, width - 176f, 42f);
            PanelKit.EnableBestFit(name, 18);
            Text meta = ModalPolish038Chrome.Text(body, "DeepTalkMeta",
                $"{MemberRace(member, memberIndex)} · {member.Career}", 15,
                ModalPolish038Chrome.Accent, FontStyle.Bold, TextAnchor.MiddleLeft);
            PanelKit.PlaceTop(meta.rectTransform, 168f, 54f, width - 176f, 28f);
            PanelKit.EnableBestFit(meta, 12);

            Text affectionGain = ModalPolish038Chrome.Text(body, "DeepTalkAffectionResult",
                $"好感度 {affectionBefore} → {affectionAfter}（+{affectionAfter - affectionBefore}） · 明日可再来",
                15, new Color32(111, 255, 194, 255), FontStyle.Bold, TextAnchor.MiddleLeft);
            PanelKit.PlaceTop(affectionGain.rectTransform, 168f, 96f, width - 176f, 56f);
            PanelKit.EnableBestFit(affectionGain, 12);

            ModalPolish038Chrome.Divider(body, "DeepTalkDivider", 0f, 216f, width);
            Text dialogueText = ModalPolish038Chrome.Text(body, "DeepTalkDialogue", dialogue, 16,
                ModalPolish038Chrome.TextWhite, FontStyle.Normal, TextAnchor.UpperLeft);
            PanelKit.PlaceTop(dialogueText.rectTransform, 4f, 230f, width - 8f, height - 330f);
            dialogueText.lineSpacing = 1.25f;

            GameObject back = ModalPolish038Chrome.ActionButton(body, "BackToProfile", "返回档案", 18,
                () => OpenTeamMember(memberIndex, teamSlot), ModalPolish038ButtonStyle.Primary);
            PanelKit.PlaceTop(back.GetComponent<RectTransform>(), (width - 280f) * .5f,
                height - 72f, 280f, 56f);
        }
    }
}
