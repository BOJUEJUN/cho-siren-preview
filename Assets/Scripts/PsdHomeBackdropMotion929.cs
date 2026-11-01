using UnityEngine;
using UnityEngine.UI;
namespace ChoSiren
{
    /// <summary>Loops only the authored background typography and record, behind every actor and UI.</summary>
    public sealed class PsdHomeBackdropMotion929 : MonoBehaviour
    {
        RectTransform disc,logo;Image[] copies;float clock,period;
        // The lettering's centre line slopes down to the right in the authored raster.
        // Translate along that line, without rotating or stretching the original artwork.
        static readonly Vector2 TitleTrack = new Vector2(1f,-.1f);
        public RectTransform TitleWindow { get; private set; }
        public void SetDisc(RectTransform value)
        {
            disc=value;ChoSiren.Panels.PanelKit.CenterPivot(disc);
        }
        public void SetLogo(Image image)
        {
            logo=image.rectTransform;
            var parent=logo.parent;int sibling=logo.GetSiblingIndex();
            var window=new GameObject("BackdropTitleWindow",typeof(RectTransform),typeof(RectMask2D));
            TitleWindow=(RectTransform)window.transform;
            TitleWindow.SetParent(parent,false);TitleWindow.SetSiblingIndex(sibling);
            TitleWindow.anchorMin=logo.anchorMin;TitleWindow.anchorMax=logo.anchorMax;
            TitleWindow.pivot=logo.pivot;TitleWindow.sizeDelta=logo.sizeDelta;
            TitleWindow.anchoredPosition=logo.anchoredPosition;
            TitleWindow.localRotation=logo.localRotation;TitleWindow.localScale=logo.localScale;
            period=logo.rect.width;
            logo.SetParent(TitleWindow,false);
            logo.anchorMin=logo.anchorMax=logo.pivot=new Vector2(0,1);
            logo.anchoredPosition=Vector2.zero;logo.localScale=Vector3.one;logo.localRotation=Quaternion.identity;
            copies=new Image[2];copies[0]=image;
            for(int i=1;i<copies.Length;i++)
            {
                var obj=new GameObject("BackdropTitleRepeat"+i,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
                obj.transform.SetParent(TitleWindow,false);
                var r=(RectTransform)obj.transform;r.anchorMin=logo.anchorMin;r.anchorMax=logo.anchorMax;r.pivot=logo.pivot;r.sizeDelta=logo.sizeDelta;
                var copy=obj.GetComponent<Image>();copy.sprite=image.sprite;copy.color=image.color;copy.raycastTarget=false;copies[i]=copy;
            }
            Advance(0);
        }
        void Update()=>Advance(Time.unscaledDeltaTime);
        public void Advance(float seconds)
        {
            if(BattlePresentationOptions.ReduceMotion)
            {
                if(disc!=null)disc.localRotation=Quaternion.identity;
                if(logo!=null)logo.anchoredPosition=Vector2.zero;
                if(copies!=null)for(int i=1;i<copies.Length;i++)copies[i].enabled=false;
                return;
            }
            clock+=Mathf.Max(0,seconds);
            if(disc!=null)disc.localEulerAngles=new Vector3(0,0,-clock*7);
            if(copies!=null)for(int i=0;i<copies.Length;i++)
            {
                copies[i].enabled=true;
                // The local mask stays at the PSD bounds. No repeat can leak across the
                // right-hand record, mail or settings, even while a word crosses the seam.
                copies[i].rectTransform.anchoredPosition=TitleTrack*(i*period-Mathf.Repeat(clock*22,period));
            }
        }
    }
}
