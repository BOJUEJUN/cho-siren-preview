using System;
using UnityEngine;
namespace ChoSiren
{
    // Offline measured alpha bounds avoid runtime texture readbacks and transparent-padding size bias.
    public static class BattleSilhouette929
    {
        [Serializable] sealed class Bounds { public string id; public float x,y,width,height; }
        [Serializable] sealed class Catalog { public Bounds[] entries; }
        static Catalog catalog;
        public static Sprite Trim(Sprite original,string id)
        {
            if(original==null)return null;
            if(catalog==null)
            {
                var json=Resources.Load<TextAsset>("BattleEnemyBounds929");
                catalog=json==null?new Catalog():JsonUtility.FromJson<Catalog>(json.text);
            }
            if(catalog.entries==null)return original;
            foreach(var b in catalog.entries)
            {
                if(b.id!=id)continue;
                Rect r=original.rect;
                var crop=new Rect(r.x+b.x*r.width,r.y+b.y*r.height,b.width*r.width,b.height*r.height);
                var result=Sprite.Create(original.texture,crop,Vector2.one*.5f,original.pixelsPerUnit,0,SpriteMeshType.FullRect);
                result.name=original.name+"-VisibleSilhouette";return result;
            }
            return original;
        }
    }
}
