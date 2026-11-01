using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 2026-10-31 (赵金 lobby batch): the 「开始演出」 green brush edge on the lobby's top-right column and the
    /// bottom navigation. Clean at rest; the current tab holds a steady edge, any press flares once.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        // Each top-right button's own artwork layer; the edge follows that silhouette.
        private static readonly (string action, string layer)[] LobbyFlameButtons1031 =
        {
            ("Settings", "layer-50"), ("Mail", "layer-49"), ("HideUi", "layer-eye"),
        };

        private static void AttachLobbyFlames1031(Dictionary<string, GameObject> buttons)
        {
            foreach (var (action, layer) in LobbyFlameButtons1031)
            {
                if (!buttons.TryGetValue(action, out GameObject host)) continue;
                Image art = host.transform.Find(layer + "VisualV2")?.GetComponent<Image>();
                UI.InkFlameEdge1031.AttachToButton(host.GetComponent<Button>(), art, .2f);
            }
        }

        /// <summary>The selected tab lights (one burst, then steady); every tab flares when pressed.</summary>
        private static void AttachNavigationFlame1031(Button button, Image icon, bool selected)
        {
            UI.InkFlameEdge1031 edge = UI.InkFlameEdge1031.AttachToButton(button, icon, .19f);
            if (edge != null && selected) edge.Ignite();
        }
    }
}
