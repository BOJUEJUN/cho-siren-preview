using System.Collections.Generic;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    /// <summary>
    /// 2026-11-12 七色功能骰 in battle: each carried die is a team-skill button stacked above 应急守护 (gem-tinted die face and
    /// its name). Once a battle each, at most two a round; 自动 spends one a round by itself.
    /// </summary>
    public sealed partial class TacticsBattlePanel
    {
        private sealed class RelicButton1111
        {
            public int Slot;
            public Button Button;
            public Image Die;
            public Text Label;
            public CanvasGroup Group;
        }

        private readonly List<RelicButton1111> relicButtons1111 = new List<RelicButton1111>();

        private void BuildRelicButtons1111(Transform parent)
        {
            if (parent == null || battle == null || battle.RelicDice.Count == 0 || relicButtons1111.Count > 0) return;
            for (int slot = 0; slot < battle.RelicDice.Count; slot++)
            {
                string id = battle.RelicDice[slot];
                int captured = slot;
                var root = kit.NewRect("RelicDie1111-" + slot, parent);
                PsdPlace(root, 2590 - slot * 370, 2230, 350, 370); // a row just above 应急守护
                var group = root.gameObject.AddComponent<CanvasGroup>();
                var hit = kit.NewImage("RelicHit", root, PunkUi.SlantFill, new Color32(10, 6, 26, 190));
                PanelKit.Stretch(hit.rectTransform);
                hit.raycastTarget = true;
                Image die = kit.NewImage("RelicFace", root, Resources.Load<Sprite>(RelicDice1111.SpritePath(id, 6)), Color.white);
                die.preserveAspect = true; die.raycastTarget = false;
                PanelKit.PlaceTop(die.rectTransform, 13, 0, 60, 60);
                Text label = kit.NewPlacedText(root, RelicDice1111.Name(id), 17, Color.white, -6, 60, 98, 26,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                label.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, .9f);
                var button = root.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => UseRelic1111(captured));
                relicButtons1111.Add(new RelicButton1111 { Slot = slot, Button = button, Die = die, Label = label, Group = group });
            }
            RefreshRelicButtons1111();
        }

        private void UseRelic1111(int slot)
        {
            if (paused || closing || !battle.TryUseRelic(slot)) { RefreshRelicButtons1111(); return; }
            string id = battle.RelicDice[slot];
            commandFeedback = RelicDice1111.ColorName(id) + "·" + RelicDice1111.Name(id);
            commandFeedbackUntil = battle.ElapsedMilliseconds + 3000;
            Sfx("cut", .7f);
            RefreshAllCells();
            RefreshRealtimeCommands();
        }

        private void RefreshRelicButtons1111()
        {
            foreach (RelicButton1111 b in relicButtons1111)
            {
                bool used = battle.RelicUsed(b.Slot);
                bool ready = battle.CanUseRelic(b.Slot);
                b.Button.interactable = ready;
                b.Group.alpha = used ? .3f : ready ? 1f : .6f;
            }
        }
    }
}
