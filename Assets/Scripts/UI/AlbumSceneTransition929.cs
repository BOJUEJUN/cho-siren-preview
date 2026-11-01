using System;
using UnityEngine;
using UnityEngine.UI;
using ChoSiren.UI;
namespace ChoSiren
{
    /// <summary>One monochrome iris carries live type, the current portrait and a translucent glass record.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AlbumSceneTransition929 : MaskableGraphic
    {
        Action covered; float elapsed; bool switched;
        RectTransform sleeve; Image silhouette; CanvasGroup artwork; Material portraitMaterial;
        public const float Duration=.76f;
        public const float CoverTime=.29f;
        public bool HasSwitched=>switched;
        public bool ManualPlayback { get; set; }
        public bool FullyCovered=>elapsed>=.27f && elapsed<=.37f;
        public static readonly Color Blue=new Color32(137,62,255,255);
        public static readonly Color Ink=new Color32(9,12,41,255);
        public static readonly Color Paper=new Color32(241,246,255,255);

        public static AlbumSceneTransition929 Play(Transform parent,Action switchScene,Sprite portrait=null)
        {
            var existing=parent.GetComponentInChildren<AlbumSceneTransition929>();if(existing!=null)return existing;
            var go=new GameObject("AlbumSceneTransition",typeof(RectTransform),typeof(CanvasRenderer),typeof(AlbumSceneTransition929));
            go.transform.SetParent(parent,false);var fx=go.GetComponent<AlbumSceneTransition929>();
            fx.rectTransform.anchorMin=Vector2.zero;fx.rectTransform.anchorMax=Vector2.one;fx.rectTransform.offsetMin=fx.rectTransform.offsetMax=Vector2.zero;
            fx.covered=switchScene;fx.raycastTarget=true;
            // Both the incoming sleeve and outgoing aperture mask every decorative layer.
            go.AddComponent<Mask>().showMaskGraphic=true;
            var art=new GameObject("RecordSleeve",typeof(RectTransform),typeof(CanvasRenderer),typeof(CanvasGroup),typeof(AlbumTransitionSleeve929));
            art.transform.SetParent(go.transform,false);fx.sleeve=(RectTransform)art.transform;
            fx.sleeve.anchorMin=Vector2.zero;fx.sleeve.anchorMax=Vector2.one;fx.sleeve.offsetMin=fx.sleeve.offsetMax=Vector2.zero;
            art.GetComponent<AlbumTransitionSleeve929>().raycastTarget=false;fx.artwork=art.GetComponent<CanvasGroup>();fx.artwork.blocksRaycasts=false;
            if(portrait==null)portrait=ReferenceArt038.Load("Art/Reference038/home-layer-04");
            var record=SirenTransitionDisc.Add(fx.sleeve,"TransitionGlassRecord");
            record.rectTransform.anchorMin=new Vector2(-.26f,.22f);
            record.rectTransform.anchorMax=new Vector2(1.06f,.88f);
            record.rectTransform.offsetMin=record.rectTransform.offsetMax=Vector2.zero;
            fx.silhouette=fx.AddImage("CurrentCharacterSilhouette",portrait,Color.white);
            // Keep the actual approved portrait; duotone lighting must not erase its identity.
            // One restrained offset repeats the cut-paper profile; no blur or bloom.
            var rim=fx.AddImage("SilhouettePaperEdge",portrait,new Color32(247,245,255,130));rim.transform.SetSiblingIndex(0);
            var font=Resources.Load<Font>("Fonts/NotoSansSC-Subset") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            fx.AddType("SideLabel","CHO / SIREN     •     SIDE A",font,19,new Vector2(.08f,.9f),new Vector2(.9f,.045f));
            fx.AddType("StartType","开始",font,126,new Vector2(.045f,.215f),new Vector2(1.1f,.12f));
            fx.AddType("LiveType","演出",font,170,new Vector2(.22f,.085f),new Vector2(1.1f,.145f));
            fx.AddType("ScoreLabel","这一刻，轮到你闪耀",font,22,new Vector2(.09f,.045f),new Vector2(.88f,.035f));
            fx.Advance(0);return fx;
        }
        Image AddImage(string name,Sprite sprite,Color tint)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(sleeve,false);
            var image=go.GetComponent<Image>();image.sprite=sprite;image.color=tint;image.preserveAspect=true;image.raycastTarget=false;
            var r=image.rectTransform;r.anchorMin=new Vector2(.16f,.03f);r.anchorMax=new Vector2(1.12f,.94f);r.offsetMin=r.offsetMax=Vector2.zero;
            if(name=="SilhouettePaperEdge")r.anchoredPosition=new Vector2(-8,5);
            return image;
        }
        void AddType(string name,string value,Font font,int size,Vector2 anchor,Vector2 span)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));go.transform.SetParent(sleeve,false);
            var t=go.GetComponent<Text>();t.font=font;t.text=value;t.fontSize=size;t.fontStyle=FontStyle.Bold;t.color=Paper;
            t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Overflow;t.verticalOverflow=VerticalWrapMode.Overflow;
            t.alignment=TextAnchor.MiddleLeft;var r=t.rectTransform;r.anchorMin=anchor;r.anchorMax=anchor+span;r.offsetMin=r.offsetMax=Vector2.zero;
            r.localEulerAngles=Vector3.zero;
        }
        protected override void OnDestroy(){if(portraitMaterial!=null)Destroy(portraitMaterial);base.OnDestroy();}
        void Update(){if(!ManualPlayback)Advance(Time.unscaledDeltaTime);}
        public void Advance(float seconds)
        {
            elapsed+=Mathf.Max(0,seconds);bool reduced=BattlePresentationOptions.ReduceMotion;
            if(!switched && elapsed>=(reduced?.08f:CoverTime))
            {
                switched=true;var callback=covered;covered=null;
                try{callback?.Invoke();}catch(Exception e){Debug.LogException(e);Destroy(gameObject);return;}
                transform.SetAsLastSibling();
            }
            if(artwork!=null)
            {
                artwork.alpha=reduced?0:1;
                // The record carries the composition as one piece, without shaking UI.
                sleeve.localEulerAngles=Vector3.zero;
                sleeve.localScale=Vector3.one*1.12f;
            }
            if(elapsed>=(reduced?.16f:Duration)){raycastTarget=false;gameObject.SetActive(false);Destroy(gameObject);return;}
            SetVerticesDirty();
        }
        static float Ease(float t){t=Mathf.Clamp01(t);return 1-Mathf.Pow(1-t,3);}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            if(BattlePresentationOptions.ReduceMotion)
            {var c=Ink;c.a=Mathf.Sin(Mathf.Clamp01(elapsed/.16f)*Mathf.PI);AlbumGraphicGeometry929.Quad(vh,r.min,new Vector2(r.xMin,r.yMax),r.max,new Vector2(r.xMax,r.yMin),c);return;}
            Vector2 center=r.center+new Vector2(-r.width*.22f,r.height*.12f);
            float max=0;foreach(var corner in new[]{r.min,r.max,new Vector2(r.xMin,r.yMax),new Vector2(r.xMax,r.yMin)})max=Mathf.Max(max,Vector2.Distance(center,corner));
            max+=35;
            float outer=max*Ease(elapsed/.27f),inner=max*Mathf.Pow(Mathf.Clamp01((elapsed-.37f)/.39f),1.5f);
            if(outer<=inner)return;
            AlbumGraphicGeometry929.Ring(vh,center,outer,inner,0,360,Blue,192);
        }
    }
    /// <summary>The iris stays one colour; glass and portrait live on independent masked layers.</summary>
    public sealed class AlbumTransitionSleeve929 : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r=rectTransform.rect;
            AlbumGraphicGeometry929.Quad(vh,r.min,new Vector2(r.xMin,r.yMax),r.max,
                new Vector2(r.xMax,r.yMin),AlbumSceneTransition929.Blue);
        }
    }
}
