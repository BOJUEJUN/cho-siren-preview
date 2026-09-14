using System;
using System.Collections.Generic;
using ChoSiren.Panels;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Bounded causal attack cues: a short light bolt with a comet tail, a wind-up flash at the
    /// attacker and a hit ring with light shards on the victim. Never reads or mutates battle
    /// HP, targets or clocks, and performs no per-frame allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttackTrajectoryPresentation : MonoBehaviour
    {
        public const int Capacity = 8;
        private const int ShardCount = 4;
        // Outward burst directions for the impact shards, biased along the travel axis.
        private static readonly float[] ShardAngles = { 12f, 132f, 228f, 348f };

        private sealed class Flight
        {
            public GameObject Root;
            public Image Trail, Launch, Impact;
            public RectTransform Bolt;
            public Image BoltGlow, BoltCore;
            public CombatRingGraphic Ring;
            public Image[] Shards;
            public Text Caption;
            public Vector2 Start, End;
            public Color Color;
            public float Elapsed;
            public bool Active, Heavy;
            public int PlayerIndex, Lane;
        }

        private readonly List<Flight> flights = new List<Flight>();
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

        /// <summary>
        /// playerIndex identifies the attacking player (enemyAttack=false) or targeted player
        /// (enemyAttack=true). Both endpoints use the actual visible portraits. The cue area ends
        /// above the dice, and no additional player portrait is created for the presentation.
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
            flight.Start = VisualPoint(sourceRect);
            flight.End = VisualPoint(targetRect);
            flight.PlayerIndex = playerIndex;
            flight.Color = enemyAttack ? CombatFeedbackPalette.Hurt : CombatFeedbackPalette.Attack;
            flight.Color.a = 1f;
            flight.Heavy = heavy;
            flight.Elapsed = 0f;
            flight.Active = true;
            // The causal pair is still recorded on the (hidden) label so logs/tests keep the
            // attacker → victim attribution; nothing text-shaped is drawn on the stage.
            flight.Caption.text = (sourceName ?? string.Empty) + " 攻击 " + (targetName ?? string.Empty);
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
            flight.Trail.gameObject.SetActive(flying);
            flight.Bolt.gameObject.SetActive(flying);
            flight.Launch.gameObject.SetActive(!hit);
            flight.Impact.gameObject.SetActive(hit);
            flight.Ring.gameObject.SetActive(hit);
            for (int index = 0; index < flight.Shards.Length; index++)
                flight.Shards[index].gameObject.SetActive(hit);

            // Wind-up flash at the attacker: swells during the launch window, then snaps out so
            // the read is "who fired" rather than a lingering glow.
            float windUp = Mathf.Clamp01(flight.Elapsed / launch);
            float launchAlpha = flight.Elapsed < launch ? .85f : .18f * (1f - progress);
            float launchSize = Mathf.Lerp(flight.Heavy ? 40f : 28f, flight.Heavy ? 108f : 72f, windUp);
            Place(flight.Launch.rectTransform, flight.Start, Vector2.one * launchSize);
            flight.Launch.color = WithAlpha(flight.Color, launchAlpha);

            if (flying)
            {
                Vector2 vector = head - flight.Start;
                if (vector.sqrMagnitude < .0001f) vector = Vector2.right;
                Vector2 direction = vector.normalized;
                float angle = Mathf.Atan2(-direction.y, direction.x) * Mathf.Rad2Deg;

                // Comet tail: a capped streak that trails the bolt instead of a static line
                // stretched all the way back to the attacker.
                float trailLength = Mathf.Min(vector.magnitude, flight.Heavy ? 170f : 110f);
                Vector2 tailHead = head - direction * (flight.Heavy ? 14f : 9f);
                flight.Trail.rectTransform.anchoredPosition = new Vector2(tailHead.x, -tailHead.y);
                flight.Trail.rectTransform.sizeDelta = new Vector2(trailLength, flight.Heavy ? 11f : 6f);
                flight.Trail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
                flight.Trail.color = WithAlpha(flight.Color, flight.Heavy ? .5f : .4f);

                float boltSize = (flight.Heavy ? 46f : 30f) * Mathf.Lerp(.55f, 1f,
                    Mathf.Clamp01(progress * 3f));
                flight.Bolt.anchoredPosition = new Vector2(head.x, -head.y);
                flight.Bolt.sizeDelta = Vector2.one * boltSize;
                flight.Bolt.localRotation = Quaternion.Euler(0f, 0f, angle);
                flight.BoltGlow.color = WithAlpha(flight.Color, flight.Heavy ? .6f : .5f);
                Color core = Color.Lerp(flight.Color, Color.white, flight.Heavy ? .78f : .66f);
                flight.BoltCore.color = WithAlpha(core, .95f);
                flight.BoltCore.rectTransform.sizeDelta =
                    new Vector2(boltSize * 1.35f, boltSize * (flight.Heavy ? .34f : .3f));
            }
            if (hit)
            {
                float flashSize = Mathf.Lerp(flight.Heavy ? 30f : 18f,
                    flight.Heavy ? 128f : 78f, impact);
                Place(flight.Impact.rectTransform, flight.End, Vector2.one * flashSize);
                flight.Impact.color = WithAlpha(flight.Color, (1f - impact) * .8f);

                // Expanding hit ring: quick outward ease, then the shards carry the shatter.
                float eased = 1f - (1f - impact) * (1f - impact);
                float ringSize = Mathf.Lerp(flight.Heavy ? 34f : 20f,
                    flight.Heavy ? 168f : 104f, eased);
                Place(flight.Ring.rectTransform, flight.End, Vector2.one * ringSize);
                flight.Ring.color = WithAlpha(Color.Lerp(flight.Color, Color.white, .3f),
                    (1f - impact) * (flight.Heavy ? .95f : .85f));

                for (int index = 0; index < flight.Shards.Length; index++)
                {
                    float shardAngle = ShardAngles[index] * Mathf.Deg2Rad;
                    Vector2 shardDirection = new Vector2(Mathf.Cos(shardAngle), Mathf.Sin(shardAngle));
                    float distance = impact * (flight.Heavy ? 88f : 58f);
                    Vector2 point = flight.End + shardDirection * distance;
                    float length = Mathf.Lerp(flight.Heavy ? 34f : 22f, 5f, impact);
                    Place(flight.Shards[index].rectTransform, point,
                        new Vector2(length, flight.Heavy ? 5f : 3.5f));
                    flight.Shards[index].rectTransform.localRotation =
                        Quaternion.Euler(0f, 0f, ShardAngles[index]);
                    flight.Shards[index].color = WithAlpha(
                        Color.Lerp(flight.Color, Color.white, .5f), (1f - impact) * .85f);
                }
            }
        }

        private Vector2 VisualPoint(RectTransform rect)
        {
            if (rect == null) return new Vector2(Width * .5f, Height * .35f);
            Vector3 local = stage.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
            return new Vector2(Mathf.Clamp(local.x - stage.rect.xMin, 16, Width - 16),
                Mathf.Clamp(stage.rect.yMax - local.y, 16, Height - 16));
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
            for (int index = 0; index < Capacity; index++)
            {
                RectTransform root = kit.NewRect("AttackTrajectory-" + index, layer);
                PanelKit.Stretch(root);
                var flight = new Flight { Root = root.gameObject, Lane = index };

                flight.Trail = kit.NewImage("AttackTrail", root, kit.RoundedSprite(5), Color.clear);
                flight.Trail.rectTransform.anchorMin = flight.Trail.rectTransform.anchorMax =
                    new Vector2(0f, 1f);
                flight.Trail.rectTransform.pivot = new Vector2(1f, .5f);

                flight.Launch = kit.NewImage("AttackLaunch", root, kit.RadialSprite(), Color.clear);

                // "AttackArrow" keeps its node name but is now a light bolt: a soft radial glow
                // wrapped around a hot capsule core, both rotated along the flight direction.
                flight.Bolt = kit.NewRect("AttackArrow", root);
                flight.Bolt.anchorMin = flight.Bolt.anchorMax = new Vector2(0f, 1f);
                flight.Bolt.pivot = new Vector2(.5f, .5f);
                flight.BoltGlow = kit.NewImage("BoltGlow", flight.Bolt, kit.RadialSprite(), Color.clear);
                PanelKit.Stretch(flight.BoltGlow.rectTransform);
                flight.BoltCore = kit.NewImage("BoltCore", flight.Bolt, kit.RoundedSprite(6), Color.clear);
                flight.BoltCore.rectTransform.anchorMin = flight.BoltCore.rectTransform.anchorMax =
                    new Vector2(.5f, .5f);
                flight.BoltCore.rectTransform.pivot = new Vector2(.5f, .5f);
                flight.BoltCore.rectTransform.anchoredPosition = Vector2.zero;

                flight.Impact = kit.NewImage("AttackImpact", root, kit.RadialSprite(), Color.clear);
                var ringObject = new GameObject("AttackImpactRing", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(CombatRingGraphic));
                ringObject.transform.SetParent(root, false);
                flight.Ring = ringObject.GetComponent<CombatRingGraphic>();
                flight.Ring.thickness = .16f;

                flight.Shards = new Image[ShardCount];
                for (int shard = 0; shard < ShardCount; shard++)
                    flight.Shards[shard] = kit.NewImage("AttackShard-" + shard, root,
                        kit.RoundedSprite(3), Color.clear);

                flight.Caption = kit.NewText("AttackCausalLabel", root, string.Empty, 14, PanelKit.White,
                    FontStyle.Bold, TextAnchor.MiddleCenter);
                flight.Caption.gameObject.SetActive(false);
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
