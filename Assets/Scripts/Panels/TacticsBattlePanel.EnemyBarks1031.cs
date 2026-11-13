using System;
using System.Collections.Generic;
using System.Text;
using ChoSiren.Systems.Tactics;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    /// <summary>
    /// 2026-10-31 (赵金): minions and the boss now and then throw a line in a torn-paper teal speech bubble beside
    /// their head — on entering, at low health, after knocking out one of our members and when they fall.
    /// Restraint: one bubble at a time, at least 6 s between bubble starts, ordinary taunts rarer still.
    /// Lines come from Resources/Audio/Voice/Enemy/barks.json (written by the audio pass), falling back to a
    /// built-in set. A recorded clip plays on the shared voice channel only when nothing else is speaking, and is
    /// cut whenever one of our members' skill / ultimate lines comes up, the battle pauses or sound is off.
    /// </summary>
    public sealed partial class TacticsBattlePanel
    {
        internal const float BarkMinInterval1031 = 6f;
        internal const float BarkTauntInterval1031 = 12f;
        internal const float BarkTauntChance1031 = .35f;

        private sealed class BarkLine1031 { public string Text; public string Clip; }

        private static Dictionary<string, Dictionary<string, List<BarkLine1031>>> barkTable1031;
        private EnemyBarkDriver1031 barkDriver1031;

        /// <summary>Called for every presented battle event (TacticsBattlePanel.PresentEvent).</summary>
        private void ObserveEnemyBark1031(BattleEvent entry, BattleUnit actor, BattleUnit target)
        {
            if (psdRoot == null || entry == null || closing) return;
            EnemyBarkDriver1031 driver = BarkDriver1031();
            switch (entry.Kind)
            {
                case BattleEventKind.ActionStarted:
                    if (actor == null) break;
                    if (actor.Side == BattleSide.Player)
                    {
                        // Our skill / ultimate lines always win the voice channel.
                        if (entry.SkillId == battle.ActiveSkillId(actor, true) || entry.SkillId == battle.ActiveSkillId(actor, false))
                            driver.StopVoice();
                    }
                    else if (actor.Alive) driver.Request(actor, "taunt");
                    break;
                case BattleEventKind.Spawned:
                    if (target != null && target.Side == BattleSide.Enemy) driver.Request(target, "enter");
                    break;
                case BattleEventKind.Damage:
                    if (target != null && target.Side == BattleSide.Enemy && target.Alive && target.MaxHp > 0 &&
                        target.Hp * 10 <= target.MaxHp * 3 && driver.MarkLowHealth(target.Id))
                        driver.Request(target, "lowhp");
                    break;
                case BattleEventKind.Defeated:
                    if (target == null) break;
                    if (target.Side == BattleSide.Player && actor != null && actor.Side == BattleSide.Enemy && actor.Alive)
                        driver.Request(actor, "kill");
                    else if (target.Side == BattleSide.Enemy) driver.Request(target, "death");
                    break;
            }
        }

        private EnemyBarkDriver1031 BarkDriver1031()
        {
            if (barkDriver1031 == null)
            {
                barkDriver1031 = gameObject.AddComponent<EnemyBarkDriver1031>();
                barkDriver1031.Panel = this;
            }
            return barkDriver1031;
        }

        /// <summary>Test / capture hook: shows a line from <paramref name="category"/> at once, ignoring the pacing.</summary>
        private void ForceEnemyBark1031(BattleUnit unit, string category)
        {
            if (psdRoot == null || unit == null) return;
            BarkDriver1031().Show(unit, category ?? "taunt", true);
        }

        private bool BarkBlocked1031 => paused || closing || coveredByStory || resultOverlay != null && resultOverlay.activeSelf;

        // ------------------------------------------------------------------ lines

        private BarkLine1031 PickBark1031(BattleUnit unit, string category, ref int cursor)
        {
            var table = BarkTable1031();
            bool boss = unit.Id == bossUnitId;
            List<BarkLine1031> lines = null;
            foreach (string key in new[] { unit.Definition.Id, boss ? "boss-default" : "minion-default" })
                if (table.TryGetValue(key, out var byCategory) && byCategory.TryGetValue(category, out lines) && lines.Count > 0) break;
                else lines = null;
            if (lines == null)
            {
                var builtIn = boss ? BuiltInBossBarks1031 : BuiltInMinionBarks1031;
                if (!builtIn.TryGetValue(category, out string[] texts) || texts.Length == 0) return null;
                lines = new List<BarkLine1031>();
                foreach (string text in texts) lines.Add(new BarkLine1031 { Text = text });
            }
            BarkLine1031 line = lines[Mathf.Abs(cursor) % lines.Count];
            cursor += 1 + UnityEngine.Random.Range(0, Mathf.Max(1, lines.Count - 1));
            return line;
        }

        private static Dictionary<string, Dictionary<string, List<BarkLine1031>>> BarkTable1031()
        {
            if (barkTable1031 != null) return barkTable1031;
            barkTable1031 = new Dictionary<string, Dictionary<string, List<BarkLine1031>>>(StringComparer.Ordinal);
            TextAsset asset = Resources.Load<TextAsset>("Audio/Voice/Enemy/barks");
            if (asset == null) return barkTable1031;
            try
            {
                if (!(new BarkJson1031(asset.text).Value() is Dictionary<string, object> root)) return barkTable1031;
                foreach (var enemy in root)
                {
                    if (!(enemy.Value is Dictionary<string, object> categories)) continue;
                    var byCategory = new Dictionary<string, List<BarkLine1031>>(StringComparer.Ordinal);
                    foreach (var category in categories)
                    {
                        if (!(category.Value is List<object> items)) continue;
                        var lines = new List<BarkLine1031>();
                        foreach (object item in items)
                        {
                            if (!(item is Dictionary<string, object> fields)) continue;
                            fields.TryGetValue("text", out object text);
                            fields.TryGetValue("clip", out object clip);
                            if (text is string t && t.Trim().Length > 0) lines.Add(new BarkLine1031 { Text = t.Trim(), Clip = clip as string });
                        }
                        if (lines.Count > 0) byCategory[category.Key] = lines;
                    }
                    barkTable1031[enemy.Key] = byCategory;
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning("[EnemyBarks1031] barks.json unreadable, using built-in lines: " + error.Message);
                barkTable1031.Clear();
            }
            return barkTable1031;
        }

        /// <summary>Fallback lines (idol / 魅声 stage battle), used when barks.json or a category is missing.</summary>
        private static readonly Dictionary<string, string[]> BuiltInMinionBarks1031 = new Dictionary<string, string[]>
        {
            { "enter", new[] { "台下的，安静！", "这首歌，由我来终结。", "霓虹熄灭前，先让你跑调。" } },
            { "taunt", new[] { "就这点音量？", "跟不上节拍了吧～", "噪音而已，听不见。", "再大声一点啊！" } },
            { "lowhp", new[] { "可恶……还没唱完……", "音……破了……", "不准……让我谢幕……" } },
            { "kill", new[] { "一位，退场～", "掌声呢？没有了。", "下一个轮到谁？" } },
            { "death", new[] { "这……不可能……", "我的歌……", "噪音……散了……" } },
        };

        private static readonly Dictionary<string, string[]> BuiltInBossBarks1031 = new Dictionary<string, string[]>
        {
            { "enter", new[] { "舞台是我的，观众也是。", "跪下，聆听女王的终曲。", "魅声之下，无人能与我和声。" } },
            { "taunt", new[] { "你们的和声，太廉价了。", "跟上我的调子——做不到吧？", "让我听听你们最后的高音。", "这就是全部了？真无趣。" } },
            { "lowhp", new[] { "居然……让我破音……", "终曲还没结束！", "好啊……那就燃尽这座舞台！" } },
            { "kill", new[] { "退场吧，小歌姬。", "一个声部，消失了。", "安可？不需要。" } },
            { "death", new[] { "我的……王座……", "这首歌……算你们赢了……", "灯光……熄了……" } },
        };

        // ------------------------------------------------------------------ driver

        /// <summary>Pacing, bubble placement and the voice channel for enemy lines.</summary>
        private sealed class EnemyBarkDriver1031 : MonoBehaviour
        {
            public TacticsBattlePanel Panel;
            private readonly HashSet<int> lowHealthSaid = new HashSet<int>();
            private readonly Dictionary<string, int> cursors = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly string owner = "enemy-bark:" + Guid.NewGuid().ToString("N");
            private BarkBubble1031 bubble;
            private BattleUnit speaker;
            private float clock, lastStart = -100f, openingAt = 1.4f;
            private bool openingDone;

            public bool MarkLowHealth(int id) => lowHealthSaid.Add(id);

            public void Request(BattleUnit unit, string category)
            {
                if (unit == null || Panel == null || Panel.BarkBlocked1031) return;
                if (bubble != null && bubble.Showing) return;
                if (clock - lastStart < BarkMinInterval1031) return;
                if (category == "taunt" && (clock - lastStart < BarkTauntInterval1031 || UnityEngine.Random.value > BarkTauntChance1031)) return;
                if (category == "enter") openingDone = true;
                Show(unit, category, false);
            }

            public void Show(BattleUnit unit, string category, bool forced)
            {
                if (Panel == null || Panel.psdRoot == null) return;
                if (!Panel.EnemyHead1031(unit, out Rect head)) return;
                string key = unit.Definition.Id + ":" + category;
                cursors.TryGetValue(key, out int cursor);
                BarkLine1031 line = Panel.PickBark1031(unit, category, ref cursor);
                cursors[key] = cursor;
                if (line == null) return;
                if (bubble == null) bubble = BarkBubble1031.Create(Panel.psdRoot, Panel.kit.Font);
                StopVoice();
                float voiceSeconds = PlayVoice(line.Clip);
                float seconds = Mathf.Clamp(Mathf.Max(1.7f + line.Text.Length * .11f, voiceSeconds + .35f), 1.8f, 3.8f);
                bool rightSide = Panel.BarkOnRight1111(unit, head);
                bubble.Show(line.Text, head, rightSide, seconds, Panel.psdRoot.childCount);
                speaker = unit;
                lastStart = clock;
                openingDone = true;
            }

            private float PlayVoice(string clip)
            {
                GameAudio audio = Panel.gameAudio;
                if (string.IsNullOrEmpty(clip) || audio == null || !audio.isActiveAndEnabled || !audio.SfxEnabled ||
                    audio.MediaOwnsMusic || audio.CurrentMusicContext != GameAudio.MusicContext.Battle) return 0f;
                // Never talk over a member's line (or any other voice).
                if (audio.IsVoicePlaying) return 0f;
                const string prefix = "Voice/";
                if (!clip.StartsWith(prefix, StringComparison.Ordinal)) return 0f;
                AudioClip recorded = Resources.Load<AudioClip>("Audio/" + clip);
                if (recorded == null) return 0f;
                return audio.PlayVoice(clip.Substring(prefix.Length), owner) ? recorded.length : 0f;
            }

            public void StopVoice()
            {
                GameAudio audio = Panel != null ? Panel.gameAudio : null;
                if (audio != null) audio.StopVoice(owner);
            }

            private void Update()
            {
                if (Panel == null) return;
                bool blocked = Panel.BarkBlocked1031;
                if (blocked)
                {
                    StopVoice();
                    if (Panel.closing && bubble != null) bubble.Hide();
                    return;
                }
                GameAudio audio = Panel.gameAudio;
                if (audio != null && (!audio.SfxEnabled || audio.MediaOwnsMusic)) StopVoice();
                clock += Time.unscaledDeltaTime;
                if (!openingDone && clock >= openingAt)
                {
                    openingDone = true;
                    BattleUnit first = Panel.battle.FindUnit(Panel.bossUnitId);
                    if (first == null || !first.Alive)
                        foreach (BattleUnit unit in Panel.battle.Units)
                            if (unit.Side == BattleSide.Enemy && unit.Alive) { first = unit; break; }
                    if (first != null && first.Alive) Request(first, "enter");
                }
                if (bubble != null && bubble.Showing)
                {
                    // The bubble follows its speaker's (laid-out) head and stays above the head plates it may cross.
                    if (speaker != null && Panel.EnemyHead1031(speaker, out Rect head)) bubble.Follow(head);
                    if (bubble.transform.GetSiblingIndex() != bubble.transform.parent.childCount - 1) bubble.transform.SetAsLastSibling();
                }
            }

            private void OnDisable() => StopVoice();
            private void OnDestroy()
            {
                StopVoice();
                if (bubble != null) Destroy(bubble.gameObject);
            }

            /// <summary>Paused time does not count, so the bubble (and the pacing) freeze with the battle.</summary>
            public bool Frozen => Panel == null || Panel.BarkBlocked1031;
        }

        // ------------------------------------------------------------------ bubble

        /// <summary>Torn-paper pop bubble: ink shadow, white torn edge, teal sheet, a magenta tape strip and an ink tail.</summary>
        private sealed class BarkBubble1031 : MonoBehaviour
        {
            private const float MaxTextWidth = 236f;
            private RectTransform root, body;
            private Graphic shadow, edge, paper, tape;
            private BarkTail1031 tailShadow, tail;
            private Text label;
            private CanvasGroup group;
            private float age = -1f, life;
            private bool rightSide;
            private Vector2 size;
            private EnemyBarkDriver1031 driver;

            public bool Showing => age >= 0f;

            public static BarkBubble1031 Create(RectTransform parent, Font font)
            {
                var go = new GameObject("EnemyBark1031", typeof(RectTransform), typeof(CanvasGroup));
                go.transform.SetParent(parent, false);
                var b = go.AddComponent<BarkBubble1031>();
                b.Build(font);
                go.SetActive(false);
                return b;
            }

            private void Build(Font font)
            {
                root = (RectTransform)transform;
                root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
                root.pivot = new Vector2(0f, 0f);
                group = GetComponent<CanvasGroup>();
                group.blocksRaycasts = false; group.interactable = false;
                body = new GameObject("Body", typeof(RectTransform)).GetComponent<RectTransform>();
                body.SetParent(root, false);
                Stretch(body);
                tailShadow = Layer<BarkTail1031>("TailShadow", new Color32(14, 8, 20, 235));
                shadow = Layer<TornPaper1025>("Shadow", new Color32(14, 8, 20, 235));
                edge = Layer<TornPaper1025>("Edge", new Color32(248, 245, 255, 255));
                tail = Layer<BarkTail1031>("Tail", Brand1025.Teal);
                paper = Layer<TornPaper1025>("Paper", Brand1025.Teal);
                tape = Layer<Image>("Tape", new Color32(255, 70, 196, 215));
                label = new GameObject("Line", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
                label.transform.SetParent(body, false);
                label.font = font; label.fontSize = 20; label.fontStyle = FontStyle.BoldAndItalic;
                label.color = Brand1025.Ink; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
                label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow;
                label.lineSpacing = .92f;
            }

            private T Layer<T>(string name, Color color) where T : Graphic
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
                go.transform.SetParent(body, false);
                T g = go.GetComponent<T>();
                g.color = color; g.raycastTarget = false;
                return g;
            }

            private static void Stretch(RectTransform rect)
            {
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                rect.pivot = new Vector2(.5f, .5f);
            }

            public void Show(string text, Rect head, bool toTheRight, float seconds, int siblingIndex)
            {
                driver = GetComponentInParent<TacticsBattlePanel>()?.barkDriver1031;
                gameObject.SetActive(true);
                transform.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, transform.parent.childCount - 1));
                label.text = text;
                label.rectTransform.sizeDelta = new Vector2(MaxTextWidth, 10f);
                float textWidth = Mathf.Min(MaxTextWidth, label.preferredWidth + 2f);
                label.rectTransform.sizeDelta = new Vector2(textWidth, 10f);
                float textHeight = label.preferredHeight;
                size = new Vector2(textWidth + 44f, textHeight + 28f);
                root.sizeDelta = size;
                Place(shadow.rectTransform, new Vector2(6f, -6f), Vector2.zero);
                Place(edge.rectTransform, Vector2.zero, new Vector2(5f, 5f));
                Place(paper.rectTransform, Vector2.zero, Vector2.zero);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .5f);
                label.rectTransform.anchoredPosition = new Vector2(0f, 1f);
                label.rectTransform.sizeDelta = new Vector2(textWidth, textHeight + 4f);
                var tapeRect = tape.rectTransform;
                tapeRect.anchorMin = tapeRect.anchorMax = new Vector2(toTheRight ? 1f : 0f, 1f);
                tapeRect.pivot = new Vector2(.5f, .5f);
                tapeRect.sizeDelta = new Vector2(46f, 13f);
                tapeRect.anchoredPosition = new Vector2(toTheRight ? -14f : 14f, -2f);
                tapeRect.localEulerAngles = new Vector3(0f, 0f, toTheRight ? -24f : 24f);
                rightSide = toTheRight;
                tail.rectTransform.anchorMin = tailShadow.rectTransform.anchorMin = Vector2.zero;
                tail.rectTransform.anchorMax = tailShadow.rectTransform.anchorMax = Vector2.zero;
                tail.rectTransform.pivot = tailShadow.rectTransform.pivot = Vector2.zero;
                tail.rectTransform.sizeDelta = tailShadow.rectTransform.sizeDelta = size;
                tail.rectTransform.anchoredPosition = Vector2.zero;
                tailShadow.rectTransform.anchoredPosition = new Vector2(5f, -5f);
                life = seconds;
                age = 0f;
                Follow(head);
                Pose();
            }

            private void Place(RectTransform rect, Vector2 offset, Vector2 inflate)
            {
                Stretch(rect);
                rect.offsetMin = offset - inflate;
                rect.offsetMax = offset + inflate;
            }

            /// <summary>Keeps the bubble beside the head, on the stage side away from the name plate, level with it.</summary>
            public void Follow(Rect head)
            {
                float gap = Mathf.Clamp(head.width * .12f, 22f, 56f);
                float x = rightSide ? head.x + gap : head.x - gap - size.x;
                // Level with the face (canvas units from the top of the stage), its lower edge a little under the eyes.
                float bottom = head.y + size.y * .3f;
                x = Mathf.Clamp(x, 6f, 714f - size.x);
                bottom = Mathf.Max(bottom, 186f + size.y);
                root.anchoredPosition = new Vector2(x, -bottom);
                // The tail leaves the head-side edge low down and points at the face.
                Vector2 face = new Vector2(head.x - x, bottom - head.y);
                float edgeX = rightSide ? 6f : size.x - 6f;
                Vector2 a = new Vector2(edgeX, size.y * .16f), b = new Vector2(edgeX, size.y * .46f);
                Vector2 tip = Vector2.Lerp(new Vector2(edgeX, size.y * .2f), face, .5f);
                float reach = Mathf.Clamp(Mathf.Abs(tip.x - edgeX), 14f, 26f);
                tip.x = edgeX + (rightSide ? -reach : reach);
                tip.y = Mathf.Clamp(tip.y, -8f, size.y * .3f);
                tail.SetTriangle(a, b, tip);
                tailShadow.SetTriangle(a, b, tip);
            }

            public void Hide()
            {
                age = -1f;
                gameObject.SetActive(false);
            }

            private void Update()
            {
                if (age < 0f) return;
                if (driver != null && driver.Frozen) return;
                age += Time.unscaledDeltaTime;
                if (age >= life) { Hide(); return; }
                Pose();
            }

            private void Pose()
            {
                bool calm = BattlePresentationOptions.ReduceMotion;
                const float pop = .22f, fade = .18f;
                float scale = 1f, alpha = 1f, tilt = rightSide ? -2.5f : 2.5f;
                if (age < pop)
                {
                    float p = age / pop;
                    alpha = Mathf.Clamp01(p * 2.2f);
                    // Slaps on: overshoots to 1.1 and settles, with a small counter-rotation.
                    if (!calm)
                    {
                        scale = p < .6f ? Mathf.Lerp(.35f, 1.1f, 1f - (1f - p / .6f) * (1f - p / .6f)) : Mathf.Lerp(1.1f, 1f, (p - .6f) / .4f);
                        tilt += (1f - p) * (rightSide ? 7f : -7f);
                    }
                }
                else if (age > life - fade)
                {
                    float q = Mathf.Clamp01((life - age) / fade);
                    alpha = q;
                    if (!calm) scale = Mathf.Lerp(.9f, 1f, q);
                }
                else if (!calm) scale = 1f + Mathf.Sin((age - pop) * 5.2f) * .006f;
                group.alpha = alpha;
                body.pivot = new Vector2(rightSide ? 0f : 1f, 0f);
                body.anchoredPosition = Vector2.zero;
                body.localScale = Vector3.one * scale;
                body.localEulerAngles = new Vector3(0f, 0f, tilt);
            }
        }

        /// <summary>A plain triangle (the bubble's tail), in its own rect's local coordinates.</summary>
        private sealed class BarkTail1031 : MaskableGraphic
        {
            private Vector2 a, b, tip;

            public void SetTriangle(Vector2 left, Vector2 right, Vector2 point)
            {
                a = left; b = right; tip = point;
                SetVerticesDirty();
            }

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                Rect r = rectTransform.rect;
                Vector2 o = new Vector2(r.xMin, r.yMin);
                Color32 c = color;
                vh.AddVert(o + a, c, Vector2.zero);
                vh.AddVert(o + b, c, Vector2.zero);
                vh.AddVert(o + tip, c, Vector2.zero);
                vh.AddTriangle(0, 1, 2);
            }
        }

        // ------------------------------------------------------------------ json

        /// <summary>Minimal JSON reader for barks.json (objects, arrays, strings, numbers, true/false/null).</summary>
        private sealed class BarkJson1031
        {
            private readonly string s;
            private int i;
            public BarkJson1031(string text) { s = text ?? string.Empty; }

            public object Value()
            {
                Skip();
                if (i >= s.Length) throw new FormatException("unexpected end");
                char c = s[i];
                if (c == '{') return Obj();
                if (c == '[') return Arr();
                if (c == '"') return Str();
                int start = i;
                while (i < s.Length && ",}] \t\r\n".IndexOf(s[i]) < 0) i++;
                string word = s.Substring(start, i - start);
                if (word == "null") return null;
                if (word == "true") return true;
                if (word == "false") return false;
                return double.TryParse(word, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double d)
                    ? (object)d : throw new FormatException("bad token " + word);
            }

            private Dictionary<string, object> Obj()
            {
                var map = new Dictionary<string, object>(StringComparer.Ordinal);
                i++; Skip();
                if (Peek('}')) { i++; return map; }
                while (true)
                {
                    Skip(); string key = Str(); Skip(); Expect(':');
                    map[key] = Value(); Skip();
                    if (Peek(',')) { i++; continue; }
                    Expect('}'); return map;
                }
            }

            private List<object> Arr()
            {
                var list = new List<object>();
                i++; Skip();
                if (Peek(']')) { i++; return list; }
                while (true)
                {
                    list.Add(Value()); Skip();
                    if (Peek(',')) { i++; continue; }
                    Expect(']'); return list;
                }
            }

            private string Str()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (i < s.Length && s[i] != '"')
                {
                    char c = s[i++];
                    if (c != '\\') { sb.Append(c); continue; }
                    char e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                        default: sb.Append(e); break;
                    }
                }
                Expect('"');
                return sb.ToString();
            }

            private void Skip() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
            private bool Peek(char c) => i < s.Length && s[i] == c;
            private void Expect(char c)
            {
                if (!Peek(c)) throw new FormatException($"expected '{c}' at {i}");
                i++;
            }
        }
    }
}
