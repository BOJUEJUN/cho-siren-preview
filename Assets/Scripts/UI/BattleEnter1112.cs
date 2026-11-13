using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-12 (少爷: "角色死了之后出场入场要有动画或者能看的视觉，而不是很强硬的进出"): an enemy who arrives during the
    /// fight (a new wave, a boss's adds) fades in and pops from 80% with a short overshoot instead of appearing at once.
    /// Lives on the enemy's anchor; layout keeps owning the anchor's position, this only drives alpha and scale.
    /// </summary>
    public sealed class BattleEnter1112 : MonoBehaviour
    {
        private const float Duration = .42f;
        private CanvasGroup group;
        private float age = -1f;

        public static void Show(RectTransform anchor, bool on)
        {
            if (anchor == null) return;
            bool wasOn = anchor.gameObject.activeSelf;
            anchor.gameObject.SetActive(on);
            if (!on || wasOn || !Application.isPlaying || BattlePresentationOptions.ReduceMotion) return;
            var enter = anchor.GetComponent<BattleEnter1112>() ?? anchor.gameObject.AddComponent<BattleEnter1112>();
            enter.Play();
        }

        private void Play()
        {
            group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            age = 0f;
            Apply(0f);
        }

        private void Update()
        {
            if (age < 0f) return;
            age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(age / Duration);
            Apply(t);
            if (t >= 1f) age = -1f;
        }

        private void Apply(float t)
        {
            if (group != null) group.alpha = Mathf.Clamp01(t * 2.2f);
            // 0.8 → 1.05 → 1: a small pop as she lands.
            float rise = Mathf.Clamp01(t / .7f), settle = Mathf.Clamp01((t - .7f) / .3f);
            float s = t < .7f ? Mathf.Lerp(.8f, 1.05f, 1f - Mathf.Pow(1f - rise, 3f)) : Mathf.Lerp(1.05f, 1f, settle);
            transform.localScale = new Vector3(s, s, 1f);
        }

        private void OnDisable()
        {
            if (group != null) group.alpha = 1f;
            transform.localScale = Vector3.one;
            age = -1f;
        }
    }
}
