using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// QA only ("-smokeScreen fxdemo"): a stand-alone stage shaped like the PSD battle (RectMask2D root,
    /// Anchor → Motion → Figure enemy chain, a member slot at the bottom left) that plays every Fx1023 moment
    /// in turn, so the shader effects can be captured frame by frame without entering a battle or touching the save.
    /// </summary>
    public static class FxDemo1023
    {
        public static IEnumerator Run()
        {
            var canvasGo = new GameObject("FxDemo1023", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(720f, 1536f); scaler.matchWidthOrHeight = 1f;

            RectTransform root = Rect("BattlePSD-demo", canvasGo.transform, new Vector2(720f, 1536f), Vector2.zero);
            root.gameObject.AddComponent<RectMask2D>();
            var bg = root.gameObject.AddComponent<RawImage>(); bg.color = Color.white; bg.texture = Load("fc645c95b3956bea-stage.png");
            if (bg.texture == null) bg.color = new Color(.12f, .07f, .2f, 1f);

            RectTransform anchor = Rect("EnemyAnchor-demo", root, new Vector2(400f, 750f), new Vector2(0f, 180f));
            RectTransform motion = Rect("EnemyMotion-demo", anchor, new Vector2(400f, 750f), Vector2.zero);
            RectTransform figure = Rect("EnemyFigure-demo", motion, new Vector2(400f, 750f), Vector2.zero);
            var figureImage = figure.gameObject.AddComponent<RawImage>(); figureImage.texture = Load("672301ac2cc2e12e-enemy-0.png");
            RectTransform caster = Rect("MemberSlot-demo", root, new Vector2(170f, 330f), new Vector2(-250f, -560f));

            RectTransform layer = Rect("Fx1023", root, new Vector2(720f, 1536f), Vector2.zero);
            Fx1023 fx = Fx1023.Ensure(layer, root);

            yield return new WaitForSecondsRealtime(1f);
            // -fxdemoOnly ult: just the ultimates (frame captures start at a fixed delay)
            bool ultOnly = SmokeCaptureBootstrap.Argument("-fxdemoOnly") == "ult";
            foreach (string race in ultOnly ? new string[0] : new[] { "charm", "demon", "mermaid", "bloodelf" })
            {
                Debug.Log("CHO_SIREN_FXDEMO skill " + race);
                fx.Skill(race, figure, caster, 1f, true);
                yield return new WaitForSecondsRealtime(2.4f);
            }
            if (!ultOnly)
            {
                Debug.Log("CHO_SIREN_FXDEMO crit"); fx.Crit("charm", figure); yield return new WaitForSecondsRealtime(1.3f);
                Debug.Log("CHO_SIREN_FXDEMO slash"); fx.Slash("demon", figure); yield return new WaitForSecondsRealtime(1f);
            }
            fx.ImpactFrames = false;   // the game turns the P5 negative off for ultimates (ImpactFrame1022 owns it)
            foreach (string race in new[] { "charm", "bloodelf" })
            {
                Debug.Log("CHO_SIREN_FXDEMO ult " + race); fx.Ultimate(race, figure); yield return new WaitForSecondsRealtime(3f);
            }
            Debug.Log("CHO_SIREN_FXDEMO defeat"); fx.Defeat(figure); yield return new WaitForSecondsRealtime(1.2f);
            Debug.Log("CHO_SIREN_FXDEMO done");
        }

        static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false); rt.sizeDelta = size; rt.anchoredPosition = pos;
            return rt;
        }

        static Texture2D Load(string file)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "BattlePsd928", file);
            if (!File.Exists(path)) return null;
            var tex = new Texture2D(2, 2);
            return tex.LoadImage(File.ReadAllBytes(path)) ? tex : null;
        }
    }
}
