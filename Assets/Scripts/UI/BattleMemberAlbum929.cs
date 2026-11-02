using System;
using ChoSiren.Panels;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>Authored sleeve states. Only the visual children move; the card hit target stays fixed.</summary>
    public sealed class BattleMemberAlbum929 : MonoBehaviour
    {
        const float Scale = 720f / 2946f;
        Image portrait, disc, glow, passive;
        Outline outline;
        CanvasGroup passiveGroup;
        Func<bool> ready, paused;
        float amount, phase, spin, passiveTime = -1;
        float portraitRatio = .63f;
        Sprite portraitSource, framedPortrait;
        Rect? portraitArea;
        public float Expansion => amount;
        public bool PassiveVisible => passiveTime >= 0;
        // 2026-10-25 disc swipe: the record follows the finger up (drag), springs back, or flies off (launch).
        const float LaunchSeconds = .36f, LaunchRise = 900f, LaunchWait = 3f;
        float drag, launch = -1f, launchedAt = -1f, launchFrom;
        bool holding;
        /// <summary>True from a launch until her ultimate is spent (the record stays gone meanwhile).</summary>
        public bool Launched => launch >= 0f || launchedAt >= 0f;
        public RectTransform Disc => disc.rectTransform;
        /// <summary>Optional top-left canvas bounds; the authored default remains unchanged.</summary>
        public void SetPortraitArea(Rect bounds)
        {
            portraitArea = bounds;
            Pose();
        }
        internal void Configure(Image actor, Image record, Image halo, Outline frame,
            Func<bool> isReady, Func<bool> isPaused, PanelKit kit)
        {
            portrait=actor;disc=record;glow=halo;outline=frame;ready=isReady;paused=isPaused;
            var pop=kit.NewRect("PassivePortrait",transform);
            PanelKit.PlaceTop(pop, 28, -92, 112, 124);
            passiveGroup=pop.gameObject.AddComponent<CanvasGroup>();passiveGroup.blocksRaycasts=false;passiveGroup.interactable=false;passiveGroup.alpha=0;
            passive=kit.NewImage("Portrait",pop,null,Color.white);PanelKit.Stretch(passive.rectTransform);passive.preserveAspect=true;passive.raycastTarget=false;
            kit.AddOutline(passive.gameObject,new Color32(177,235,255,190),1.2f);
            Pose();
        }
        /// <summary>Finger lift in canvas units while swiping the record out of the sleeve.</summary>
        public void SetDrag(float lift) { holding = true; drag = Mathf.Max(0f, lift); Pose(); }
        public void ReleaseDrag() => holding = false;
        public void PlayLaunch()
        {
            holding = false;
            if (BattlePresentationOptions.ReduceMotion) { launch = -1f; launchedAt = Time.unscaledTime; drag = 0f; Pose(); return; }
            launchFrom = drag; launch = 0f; launchedAt = -1f;
        }
        public void SetPortrait(Sprite sprite)
        {
            if (sprite != null)
            {
                if (portraitSource != sprite)
                {
                    if (framedPortrait != null) Destroy(framedPortrait);
                    portraitSource = sprite;
                    framedPortrait = FrameCardBust(sprite);
                }
                Sprite shown = framedPortrait != null ? framedPortrait : sprite;
                portrait.sprite = shown;
                portraitRatio = shown.rect.width / shown.rect.height;
            }
            Pose();
        }
        // The battle crop includes wide microphones/arms. Fitting that whole width used to
        // shrink Nightingale's face more than the others. Trim only the side margins around
        // the existing face anchor; keep the complete native top edge and original alpha.
        static Sprite FrameCardBust(Sprite sprite)
        {
            const string suffix = "-BattleCloseup";
            if (!sprite.name.EndsWith(suffix, StringComparison.Ordinal)) return null;
            string memberId = sprite.name.Substring(0, sprite.name.Length - suffix.Length);
            if (!UI.PortraitFace1101.TryMemberFace(memberId, out Rect face, out _)) return null;
            Sprite source = PanelKit.MemberSpriteOrNull(memberId, false);
            if (source == null || source.texture != sprite.texture) return null;
            Rect crop = sprite.rect, original = source.rect;
            float faceWidth = face.width * original.width;
            float width = Mathf.Min(crop.width, Mathf.Max(crop.height * .82f, faceWidth * 2.75f));
            if (width >= crop.width - .5f) return null;
            float centre = original.x + face.center.x * original.width;
            crop.x = Mathf.Clamp(centre - width * .5f, crop.xMin, crop.xMax - width);
            crop.width = width;
            Sprite framed = Sprite.Create(sprite.texture, crop, Vector2.one * .5f,
                sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            framed.name = sprite.name + "-CardFramed";
            return framed;
        }
        public void PlayPassive(Sprite sprite)
        {
            if(!isActiveAndEnabled || sprite==null)return;
            passive.sprite=sprite;passiveTime=0;passive.transform.parent.SetAsLastSibling();
        }
        void LateUpdate()=>Advance(Time.unscaledDeltaTime);
        public void Advance(float seconds)
        {
            if(portrait==null || seconds<=0 || (paused?.Invoke()??false))return;
            bool reduced=BattlePresentationOptions.ReduceMotion;
            bool isReady=ready?.Invoke()??false;
            if(launch>=0f)
            {
                launch+=seconds/LaunchSeconds;
                if(launch>=1f){launch=-1f;launchedAt=Time.unscaledTime;}
            }
            else if(launchedAt>=0f && (!isReady || Time.unscaledTime-launchedAt>LaunchWait))
            {
                // spent (or the cast never came): the record is back home, hidden in the sleeve when not ready
                launchedAt=-1f;drag=0f;if(!isReady)amount=0f;
            }
            if(!holding && launch<0f && launchedAt<0f)drag=reduced?0f:Mathf.Lerp(drag,0f,1f-Mathf.Exp(-seconds*16f));
            float target=isReady?1:0;
            amount=reduced?target:Mathf.MoveTowards(amount,target,seconds/(target>0?.32f:.22f));
            phase+=seconds;
            float charge=Mathf.Clamp01(drag/110f);
            if(amount>0 && !reduced)spin+=seconds*42*amount*(1f+3f*charge+(launch>=0f?8f:0f));
            if(passiveTime>=0)
            {
                passiveTime+=seconds;
                if(passiveTime>=.72f)passiveTime=-1;
            }
            Pose();
        }
        void Pose()
        {
            if(portrait==null)return;
            float t=amount*amount*(3-2*amount);
            if (portraitArea.HasValue)
            {
                Rect area = portraitArea.Value;
                float height = area.height * Mathf.Lerp(1.06f, 1.12f, t);
                float width = height * portraitRatio;
                float limit = area.width * 1.14f;
                if (width > limit) { height *= limit / width; width = limit; }
                PanelKit.PlaceTop(portrait.rectTransform, area.x + (area.width - width) * .5f,
                    area.yMax - height, width, height);
            }
            else
            {
                float height=Mathf.Lerp(675,930,t);
                float width=height*portraitRatio;
                if(width>610){height*=610/width;width=610;}
                PanelKit.PlaceTop(portrait.rectTransform,(708-width)*.5f*Scale,(1045-height)*Scale,width*Scale,height*Scale);
            }
            // Latest PSD: the fourth disc is 228px lower than the three ready examples.
            float fly=launch>=0f?launch*launch:0f, lift=launch>=0f?launchFrom+fly*LaunchRise:drag, grow=1f+.35f*fly;
            float w=714*Scale*grow,h=630*Scale*grow;
            PanelKit.PlaceTop(disc.rectTransform,-5*Scale-(w-714*Scale)*.5f,
                (portraitArea.HasValue ? Mathf.Lerp(228,0,t) : Mathf.Lerp(268,40,t))*Scale-lift-(h-630*Scale)*.5f,
                w,h);
            PanelKit.CenterPivot(disc.rectTransform);disc.rectTransform.localEulerAngles=new Vector3(0,0,spin);
            var dc=disc.color;dc.a=launch>=0f?1f-Mathf.SmoothStep(0f,1f,(launch-.55f)/.45f):launchedAt>=0f?0f:1f;disc.color=dc;
            float pulse=BattlePresentationOptions.ReduceMotion?1:.72f+.28f*Mathf.Sin(phase*4.2f);
            glow.color=new Color(.36f,.8f,1,t*(.16f+.12f*pulse));
            outline.effectColor=new Color(.5f,.87f,1,t*(.5f+.35f*pulse));outline.effectDistance=new Vector2(1.8f,-1.8f);
            if(passiveGroup!=null)
            {
                float p=passiveTime<0?0:passiveTime/.72f;
                passiveGroup.alpha=passiveTime<0?0:Mathf.Min(p/.16f,(1-p)/.3f);
                var rect=(RectTransform)passiveGroup.transform;
                rect.anchoredPosition=new Vector2(28,-(-70-22*Mathf.Clamp01(p/.2f)));
                rect.localScale=Vector3.one*(BattlePresentationOptions.ReduceMotion?1:Mathf.Lerp(.92f,1,Mathf.Clamp01(p/.22f)));
            }
        }
        void OnDisable(){passiveTime=-1;if(passiveGroup!=null)passiveGroup.alpha=0;holding=false;launch=-1f;launchedAt=-1f;drag=0f;}
        void OnDestroy() { if (framedPortrait != null) Destroy(framedPortrait); }
    }
}
