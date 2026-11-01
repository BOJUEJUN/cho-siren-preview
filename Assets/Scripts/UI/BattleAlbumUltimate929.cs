using System;
using System.Collections.Generic;
using ChoSiren.Panels;
using UnityEngine;
using UnityEngine.UI;
namespace ChoSiren
{
    /// <summary>Brief transparent portrait accent; battlefield and controls stay visible.</summary>
    public sealed class BattleAlbumUltimate929 : MonoBehaviour
    {
        public const float Duration=1.5f;
        const float BattleViewGap=.45f;
        struct Cue { public Sprite Portrait; public float Time; }
        readonly Queue<Cue> queue=new Queue<Cue>();
        RectTransform page,portrait; CanvasGroup group; Image portraitImage;
        Func<bool> paused; PanelKit kit; float clock,elapsed,gap; int side;
        public bool IsShowing {get;private set;}
        public int PendingCount=>queue.Count;
        public void Configure(Transform host,Sprite album,Func<bool> pause)
        {
            paused=pause;kit=new PanelKit("BattlePortraitAccent");
            page=kit.NewRect("AlbumUltimate",host);PanelKit.Stretch(page);
            group=page.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;
            portraitImage=kit.NewImage("UltimatePortrait",page,null,Color.white);portrait=portraitImage.rectTransform;
            portraitImage.preserveAspect=true;portraitImage.raycastTarget=false;
            PanelKit.PlaceTop(portrait,22,275,330,540);PanelKit.CenterPivot(portrait);
            page.gameObject.SetActive(false);
        }
        public void Enqueue(Sprite art,string name,string title,Color color)
        {
            if(page==null || art==null || BattlePresentationOptions.ReduceMotion)return;
            while(queue.Count>=3)queue.Dequeue();
            queue.Enqueue(new Cue{Portrait=art,Time=clock});
            if(!IsShowing && gap<=0 && !(paused?.Invoke()??false))Next();
        }
        void Next()
        {
            while(queue.Count>0)
            {
                var c=queue.Dequeue();if(clock-c.Time>6.5f)continue;
                portraitImage.sprite=c.Portrait;elapsed=0;side=1-side;IsShowing=true;page.gameObject.SetActive(true);Pose();return;
            }
            IsShowing=false;page.gameObject.SetActive(false);
        }
        void Update()=>Advance(Time.unscaledDeltaTime*BattlePresentationOptions.Pace);
        public void Advance(float seconds)
        {
            if(page==null || seconds<=0 || (paused?.Invoke()??false))return;
            if(BattlePresentationOptions.ReduceMotion){Cancel();return;}
            clock+=seconds;
            if(!IsShowing)
            {
                gap=Mathf.Max(0,gap-seconds);if(gap<=0 && queue.Count>0)Next();return;
            }
            elapsed+=seconds;
            if(elapsed>=Duration){IsShowing=false;page.gameObject.SetActive(false);gap=BattleViewGap;return;}
            Pose();
        }
        void Pose()
        {
            float enter=1-Mathf.Pow(1-Mathf.Clamp01(elapsed/.16f),3);
            float exit=Mathf.Clamp01((elapsed-(Duration-.22f))/.22f);group.alpha=enter*(1-exit);
            float x=side==0?187:533;
            portrait.anchoredPosition=new Vector2(x+(side==0?-1:1)*(1-enter)*80,-545+exit*20);
            portrait.localScale=Vector3.one*Mathf.Lerp(.96f,1,enter);
        }
        public void Cancel(){queue.Clear();gap=0;IsShowing=false;if(page!=null)page.gameObject.SetActive(false);}
        void OnDisable()=>Cancel();
        void OnDestroy(){if(page!=null)Destroy(page.gameObject);kit?.Dispose();}
    }
}
