using UnityEngine;
using UnityEngine.UI;
using ChoSiren.Panels;

namespace ChoSiren
{
    public sealed partial class ChoSirenApp
    {
        private static void AddQuietPanelEdge(GameObject panel)
        {
            Outline edge = panel.GetComponent<Outline>() ?? panel.AddComponent<Outline>();
            edge.effectColor = new Color32(122, 160, 224, 90);
            edge.effectDistance = new Vector2(1, -1);
            edge.useGraphicAlpha = true;
        }

        private void BuildReadableMemberSkill(Transform parent, string name, string skill, string description,
            float x, Color accent)
        {
            GameObject card = NewPanel(name + "Card", parent, new Color32(12, 19, 45, 248), 14);
            PlaceTop(card.GetComponent<RectTransform>(), x, 48, 258, 192);
            AddQuietPanelEdge(card);
            var icon = SkillIconVisuals.Create(card.transform, name + "Icon", skill, description, accent);
            PlaceTop(icon.rectTransform, 12, 14, 48, 48);
            Text heading = NewPlacedText(card.transform, skill, 17, White, 72, 12, 174, 54,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            heading.name = name;
            PanelKit.EnableBestFit(heading, 14);
            Text body = NewPlacedText(card.transform, description, 15, Muted, 14, 80, 230, 98, TextAnchor.UpperLeft);
            body.name = name + "Description";
            PanelKit.EnableBestFit(body, 12);
            body.verticalOverflow = VerticalWrapMode.Truncate;
        }
    }
}
