using System;
using UnityEngine;
using UnityEngine.UI;
namespace ChoSiren
{
    /// <summary>Soft record-stage spotlights, behind every combatant; no texture or input surface.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleStageLighting929 : MaskableGraphic
    {
        Func<bool> paused; Func<int> speed; float clock;
        public void Configure(Func<bool> pause, Func<int> playbackSpeed)
        { paused=pause; speed=playbackSpeed; raycastTarget=false; }
        void Update()
        {
            if (paused != null && paused()) return;
            clock+=Time.unscaledDeltaTime*Mathf.Clamp(speed?.Invoke()??1,1,2);SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(BattlePresentationOptions.ReduceMotion)return;
            var rect=rectTransform.rect;
            // Segmented equalizer columns have independent envelopes; leave the floor readable.
            for(int i=0;i<32;i++)
            {
                float envelope=.5f+.25f*Mathf.Sin(clock*2.7f+i*.91f)+.2f*Mathf.Sin(clock*4.1f+i*1.77f);
                int segments=3+Mathf.RoundToInt(Mathf.Clamp01(envelope)*19);
                float x=rect.xMin+rect.width*(i+.5f)/32;
                float baseline=rect.yMin+rect.height*.40f;
                float step=rect.height*.018f;
                for(int j=0;j<segments;j++)
                {
                    Color c=Color.Lerp(new Color(.16f,.42f,1,.24f),new Color(.83f,.26f,1,.32f),j/22f);
                    float y=baseline+j*step;
                    AlbumGraphicGeometry929.Quad(vh,new Vector2(x-rect.width*.006f,y),new Vector2(x+rect.width*.006f,y),
                        new Vector2(x+rect.width*.006f,y+step*.72f),new Vector2(x-rect.width*.006f,y+step*.72f),c);
                }
            }
            for(int i=0;i<4;i++)
            {
                float x=rect.xMin+rect.width*(.1f+i*.27f);
                Vector2 tip=new Vector2(x,rect.yMax);
                float sway=Mathf.Sin(clock*.43f+i*1.8f)*rect.width*.16f;
                Vector2 basePoint=new Vector2(x+sway,rect.yMin);
                Color c=i%2==0?new Color(.35f,.58f,1,.17f):new Color(.88f,.38f,1,.13f);
                c.a*=.8f+Mathf.Sin(clock*1.8f+i)*.2f;
                int start=vh.currentVertCount;
                vh.AddVert(tip,c,Vector2.zero);vh.AddVert(basePoint-Vector2.right*rect.width*.22f,Color.clear,Vector2.zero);
                vh.AddVert(basePoint,new Color(c.r,c.g,c.b,c.a*.42f),Vector2.zero);
                vh.AddVert(basePoint+Vector2.right*rect.width*.22f,Color.clear,Vector2.zero);
                vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
            }
        }
    }
}
