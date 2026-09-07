using System;
using System.Collections.Generic;
using ChoSiren.Panels;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>Bounded causal attack cues. Never reads or mutates battle HP, targets or clocks.</summary>
    [DisallowMultipleComponent]
    public sealed class AttackTrajectoryPresentation : MonoBehaviour
    {
        public const int Capacity = 8;
        private sealed class Flight
        {
            public GameObject Root;
            public Image Line, SourcePortrait, SourcePulse, Impact;
            public RectTransform Arrow;
            public Text Caption;
            public Vector2 Start, End;
            public Color Color;
            public float Elapsed;
            public bool Active, Heavy;
            public int PlayerIndex, Lane;
        }

        private sealed class Proxy
        {
            public GameObject Root;
            public Image Portrait, Frame;
            public Text Name;
        }

        private readonly List<Flight> flights = new List<Flight>();
        private readonly Proxy[] proxies = new Proxy[4];
        private PanelKit kit;
        private RectTransform stage, layer;
        private Func<bool> pauseProbe;
        private Func<int> speedProbe;
        private bool configured;
        private int cursor;
        private float Width => stage != null && stage.rect.width > 0f ? stage.rect.width : 720f;
        private float Height => stage != null && stage.rect.height > 0f ? stage.rect.height : 700f;
        public int ActiveCount { get { int count = 0; foreach (Flight flight in flights) if (flight.Active) count++; return count; } }

        public void Configure(RectTransform stageRect, Func<bool> paused, Func<int> speed)
        {
            if (stageRect == null) throw new ArgumentNullException(nameof(stageRect));
            Cancel();
            stage = stageRect;
            pauseProbe = paused;
            speedProbe = speed;
            if (layer == null) Build();
            else layer.SetParent(stage, false);
            configured = true;
        }

        /// <summary>Register the original portrait for each of the four actual formation slots.</summary>
        public void SetPlayerProxy(int index, Sprite portrait, string name)
        {
            if (!configured || index < 0 || index >= proxies.Length) return;
            Proxy proxy = proxies[index];
            if (portrait != null) proxy.Portrait.sprite = portrait;
            proxy.Portrait.enabled = proxy.Portrait.sprite != null;
            proxy.Name.text = name ?? string.Empty;
            proxy.Root.SetActive(true);
            layer.gameObject.SetActive(true);
        }

        /// <summary>
        /// playerIndex identifies the attacking player (enemyAttack=false) or targeted player
        /// (enemyAttack=true). Only the enemy endpoint is read from source/targetRect; the player
        /// endpoint is always the corresponding proxy inside this stage, never the card below dice.
        /// Returns false for capacity drops; attacks are never queued and replayed late.
        /// </summary>
        public bool Play(RectTransform sourceRect, RectTransform targetRect, Sprite sourcePortrait,
            string sourceName, string targetName, Color color, bool enemyAttack, bool heavy, int playerIndex = 0)
        {
            if (!configured || !isActiveAndEnabled || playerIndex < 0 || playerIndex >= 4) return false;
            Flight flight = null;
            for (int i = 0; i < flights.Count; i++)
            {
                int index = (cursor + i) % flights.Count;
                if (flights[index].Active) continue;
                flight = flights[index];
                cursor = (index + 1) % flights.Count;
                break;
            }
            if (flight == null) return false;
            if (!enemyAttack) SetPlayerProxy(playerIndex, sourcePortrait, sourceName);
            else if (!proxies[playerIndex].Root.activeSelf)
                SetPlayerProxy(playerIndex, null, targetName);
            flight.Start = enemyAttack ? EnemyPoint(sourceRect) : PlayerPoint(playerIndex);
            flight.End = enemyAttack ? PlayerPoint(playerIndex) : EnemyPoint(targetRect);
            flight.PlayerIndex = playerIndex;
            flight.Color = enemyAttack ? Color.Lerp(color, new Color(1f, .3f, .35f, 1f), .45f) : color;
            flight.Color.a = 1f;
            flight.Heavy = heavy;
            flight.Elapsed = 0f;
            flight.Active = true;
            flight.SourcePortrait.sprite = sourcePortrait;
            flight.SourcePortrait.enabled = sourcePortrait != null;
            flight.Caption.text = (sourceName ?? string.Empty) + " 攻击 " + (targetName ?? string.Empty);
            flight.Caption.color = flight.Color;
            flight.Root.SetActive(true);
            layer.gameObject.SetActive(true);
            Draw(flight);
            return true;
        }

        public void Cancel()
        {
            foreach (Flight flight in flights)
            {
                flight.Active = false;
                if (flight.Root != null) flight.Root.SetActive(false);
            }
            if (layer != null) layer.gameObject.SetActive(false);
        }

        private void Update() => AdvancePresentation(Time.unscaledDeltaTime);

        public void AdvancePresentation(float unscaledSeconds)
        {
            if (!configured || !isActiveAndEnabled || (pauseProbe != null && pauseProbe()) ||
                unscaledSeconds <= 0f || float.IsNaN(unscaledSeconds) || float.IsInfinity(unscaledSeconds)) return;
            float delta = unscaledSeconds * Mathf.Clamp(speedProbe != null ? speedProbe() : 1, 1, 2);
            foreach (Proxy proxy in proxies)
                proxy.Frame.color = new Color32(54, 74, 108, 170);
            foreach (Flight flight in flights)
            {
                if (!flight.Active) continue;
                flight.Elapsed += delta;
                if (flight.Elapsed >= Duration(flight))
                {
                    flight.Active = false;
                    flight.Root.SetActive(false);
                }
                else Draw(flight);
            }
        }

        private static float Launch(Flight flight) => flight.Heavy ? .18f : .12f;
        private static float Travel(Flight flight) => flight.Heavy ? .36f : .28f;
        private static float Duration(Flight flight) => Launch(flight) + Travel(flight) + (flight.Heavy ? .34f : .26f);

        private void Draw(Flight flight)
        {
            float launch = Launch(flight), travel = Travel(flight);
            float progress = Mathf.Clamp01((flight.Elapsed - launch) / travel);
            Vector2 head = Vector2.Lerp(flight.Start, flight.End, progress);
            bool flying = flight.Elapsed >= launch && progress < 1f;
            bool hit = progress >= 1f;
            float impact = hit ? Mathf.Clamp01((flight.Elapsed - launch - travel) / (Duration(flight) - launch - travel)) : 0f;
            flight.Line.gameObject.SetActive(flying);
            flight.Arrow.gameObject.SetActive(flying);
            flight.SourcePulse.gameObject.SetActive(!hit);
            flight.SourcePortrait.gameObject.SetActive(flight.Elapsed < launch);
            flight.Impact.gameObject.SetActive(hit);
            Place(flight.SourcePortrait.rectTransform, flight.Start, new Vector2(32, 32));
            Place(flight.SourcePulse.rectTransform, flight.Start, Vector2.one * (flight.Heavy ? 64 : 48));
            flight.SourcePulse.color = WithAlpha(flight.Color, flight.Elapsed < launch ? .7f : .18f);
            if (flying)
            {
                Vector2 vector = head - flight.Start;
                float angle = Mathf.Atan2(-vector.y, vector.x) * Mathf.Rad2Deg;
                Place(flight.Line.rectTransform, flight.Start, new Vector2(vector.magnitude, flight.Heavy ? 4 : 2));
                flight.Line.rectTransform.pivot = new Vector2(0, .5f);
                flight.Line.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
                flight.Line.color = WithAlpha(flight.Color, .44f);
                Place(flight.Arrow, head, Vector2.one * (flight.Heavy ? 18 : 12));
                flight.Arrow.localRotation = Quaternion.Euler(0, 0, angle);
                foreach (Image image in flight.Arrow.GetComponentsInChildren<Image>()) image.color = flight.Color;
            }
            if (hit)
            {
                float size = Mathf.Lerp(flight.Heavy ? 30 : 18, flight.Heavy ? 96 : 62, impact);
                Place(flight.Impact.rectTransform, flight.End, Vector2.one * size);
                flight.Impact.color = WithAlpha(flight.Color, (1f - impact) * .85f);
            }
            // One short causal label belongs to this projectile, not the shared instant log.
            Vector2 caption = (flight.Start + flight.End) * .5f + new Vector2(0, -26 + (flight.Lane - 3.5f) * 30f);
            caption.x = Mathf.Clamp(caption.x, 110, Width - 110);
            caption.y = Mathf.Clamp(caption.y, 22, Height - 24);
            Place(flight.Caption.rectTransform, caption, new Vector2(216, 28));
            flight.Caption.color = WithAlpha(flight.Color, hit ? 1f - impact : 1f);
            proxies[flight.PlayerIndex].Frame.color = WithAlpha(flight.Color, .9f);
        }

        private Vector2 PlayerPoint(int index) => new Vector2(Width * (.125f + index * .25f), Height - 76f);

        private Vector2 EnemyPoint(RectTransform rect)
        {
            if (rect == null) return new Vector2(Width * .5f, Height * .35f);
            Vector3 local = stage.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
            return new Vector2(Mathf.Clamp(local.x - stage.rect.xMin, 52, Width - 52),
                Mathf.Clamp(stage.rect.yMax - local.y, 52, Height - 145));
        }

        private static Color WithAlpha(Color color, float alpha) { color.a = alpha; return color; }

        private static void Place(RectTransform rect, Vector2 topPoint, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(topPoint.x, -topPoint.y);
            rect.sizeDelta = size;
        }

        private void Build()
        {
            kit = new PanelKit("AttackTrajectory");
            layer = kit.NewRect("AttackTrajectoryLayer", stage);
            PanelKit.Stretch(layer);
            layer.gameObject.AddComponent<RectMask2D>();
            var group = layer.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            for (int index = 0; index < proxies.Length; index++)
            {
                GameObject root = kit.NewPanel("AttackPlayerProxy-" + index, layer, new Color32(54, 74, 108, 170), 12);
                Vector2 point = PlayerPoint(index);
                PanelKit.PlaceTop(root.GetComponent<RectTransform>(), point.x - 48, Height - 106, 96, 92);
                Image portrait = kit.NewImage("AttackProxyPortrait", root.transform, null, PanelKit.White);
                PanelKit.PlaceTop(portrait.rectTransform, 24, 6, 48, 48);
                portrait.preserveAspect = true;
                Text name = kit.NewPlacedText(root.transform, string.Empty, 14, PanelKit.White,
                    2, 59, 92, 25, TextAnchor.MiddleCenter, FontStyle.Bold);
                name.gameObject.name = "AttackProxyName";
                PanelKit.EnableBestFit(name, 12);
                proxies[index] = new Proxy { Root = root, Portrait = portrait, Frame = root.GetComponent<Image>(), Name = name };
                root.SetActive(false);
            }
            for (int index = 0; index < Capacity; index++)
            {
                RectTransform root = kit.NewRect("AttackTrajectory-" + index, layer);
                PanelKit.Stretch(root);
                var flight = new Flight { Root = root.gameObject, Lane = index };
                flight.Line = kit.NewImage("AttackTrail", root, null, Color.clear);
                flight.SourcePulse = kit.NewImage("AttackLaunch", root, kit.RadialSprite(), Color.clear);
                flight.SourcePortrait = kit.NewImage("AttackSourcePortrait", root, null, PanelKit.White);
                flight.SourcePortrait.preserveAspect = true;
                flight.Arrow = kit.NewRect("AttackArrow", root);
                for (int side = -1; side <= 1; side += 2)
                {
                    Image wing = kit.NewImage("ArrowWing" + side, flight.Arrow, null, PanelKit.White);
                    wing.rectTransform.anchorMin = wing.rectTransform.anchorMax = new Vector2(.5f, .5f);
                    wing.rectTransform.pivot = new Vector2(0, .5f);
                    wing.rectTransform.sizeDelta = new Vector2(14, 3);
                    wing.rectTransform.localRotation = Quaternion.Euler(0, 0, side * 145);
                }
                flight.Impact = kit.NewImage("AttackImpact", root, kit.RadialSprite(), Color.clear);
                flight.Caption = kit.NewText("AttackCausalLabel", root, string.Empty, 14, PanelKit.White,
                    FontStyle.Bold, TextAnchor.MiddleCenter);
                kit.AddOutline(flight.Caption.gameObject, new Color32(5, 8, 22, 245), 1.5f);
                PanelKit.EnableBestFit(flight.Caption, 12);
                flights.Add(flight);
                root.gameObject.SetActive(false);
            }
            foreach (Graphic graphic in layer.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            layer.gameObject.SetActive(false);
        }

        private void OnDisable() => Cancel();
        private void OnDestroy()
        {
            Cancel();
            if (layer != null) Destroy(layer.gameObject);
            kit?.Dispose();
        }
    }
}
