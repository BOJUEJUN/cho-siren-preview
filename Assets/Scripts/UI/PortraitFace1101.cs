using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-01 (avatar-face, 赵金 1101c): 全局立绘人脸锚点。
    /// 每张会被裁成头像/胸像/半身的立绘都在 Resources/Data/portrait-faces.json 里标了人脸框
    /// （双眼+嘴所在区域，离线动漫人脸检测 + 人工复核）。所有把立绘裁成头像的地方都走这里，
    /// 不要再各自写死偏移。
    ///
    /// 坐标约定：所有 Rect 都是相对 <b>sprite.rect</b> 的归一化坐标，y 自底向上（与 Unity UV 一致）。
    ///
    /// 用法：
    ///   image.sprite = PortraitFace1101.CropSprite(memberId, art, aspect, PortraitFace1101.Framing.Head);  // 最简单，不需要 Mask
    ///   PortraitFace1101.Apply(image, frame, memberId, Framing.Head);   // image 是带 Mask 的 frame 的子物体
    ///   PortraitFace1101.Apply(rawImage, memberId, art, Framing.Bust);  // 设 uvRect
    ///   PortraitFaceFit1101.Attach(image, memberId, Framing.Head);       // 组件：尺寸变化时自动重裁
    /// </summary>
    public static class PortraitFace1101
    {
        public enum Framing
        {
            /// <summary>头像：脸占框高约 60%，眼睛在上 40% 处。</summary>
            Head,
            /// <summary>胸像：头+肩+上胸。</summary>
            Bust,
            /// <summary>半身：到腰。</summary>
            Half,
        }

        public const string DataPath = "Data/portrait-faces";

        // 构图参数：faceFrac = 人脸框高 / 裁框高；topGap = 人脸框顶到裁框顶 / 裁框高。
        // 人脸框（眉~下巴）内眼睛约在 45% 处 → Head 下眼线 ≈ 0.16 + 0.54×0.45 ≈ 0.40，头顶留发。
        private static void Params(Framing framing, out float faceFrac, out float topGap)
        {
            switch (framing)
            {
                case Framing.Bust: faceFrac = .30f; topGap = .17f; break;   // 头顶发饰/耳朵留全
                case Framing.Half: faceFrac = .17f; topGap = .12f; break;
                default: faceFrac = .54f; topGap = .16f; break;
            }
        }

        [Serializable]
        private sealed class Entry
        {
            public string path;      // Resources 相对路径，无扩展名
            public int w, h;         // 原图像素
            public float[] face;     // x, y, w, h 归一化，y 自底向上
            public string src;       // "auto" / "manual"
            public bool cover;       // 已构图好的方形头像：整张铺满，不再按脸放大
        }

        [Serializable]
        private sealed class Table
        {
            public int version;
            public Entry[] entries;
        }

        private static Dictionary<string, Rect> byKey;
        private static Dictionary<string, Vector2> sizeByPath;
        private static HashSet<string> coverKeys;
        private static readonly Dictionary<Sprite, Rect> estimated = new Dictionary<Sprite, Rect>();
        private static readonly Dictionary<Sprite, Dictionary<string, Sprite>> cropCache = new Dictionary<Sprite, Dictionary<string, Sprite>>();

        /// <summary>数据里所有立绘的 Resources 路径（测试与工具用）。</summary>
        public static IEnumerable<string> KnownPaths
        {
            get
            {
                EnsureLoaded();
                foreach (KeyValuePair<string, Rect> pair in byKey)
                    if (pair.Key.IndexOf('/') >= 0 && pair.Key.StartsWith("Art/", StringComparison.Ordinal))
                        yield return pair.Key;
            }
        }

        public static void Reload()
        {
            byKey = null;
            estimated.Clear();
            cropCache.Clear();
        }

        private static void EnsureLoaded()
        {
            if (byKey != null) return;
            byKey = new Dictionary<string, Rect>(StringComparer.Ordinal);
            coverKeys = new HashSet<string>(StringComparer.Ordinal);
            sizeByPath = new Dictionary<string, Vector2>(StringComparer.Ordinal);
            TextAsset text = Resources.Load<TextAsset>(DataPath);
            if (text == null) return;
            Table table;
            try { table = JsonUtility.FromJson<Table>(text.text); }
            catch (Exception) { return; }
            if (table?.entries == null) return;

            var nameCount = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Entry e in table.entries)
            {
                if (e == null || string.IsNullOrEmpty(e.path)) continue;
                string name = LastSegment(e.path);
                nameCount.TryGetValue(name, out int c);
                nameCount[name] = c + 1;
            }

            foreach (Entry e in table.entries)
            {
                if (e == null || string.IsNullOrEmpty(e.path) || e.face == null || e.face.Length < 4) continue;
                var rect = new Rect(e.face[0], e.face[1], e.face[2], e.face[3]);
                string name = LastSegment(e.path);
                string parent = ParentSegment(e.path);
                Register(e.path, rect, e.cover);
                sizeByPath[e.path] = new Vector2(e.w, e.h);
                if (!string.IsNullOrEmpty(parent)) Register(parent + "/" + name, rect, e.cover);
                if (nameCount[name] == 1) Register(name, rect, e.cover);
            }
        }

        private static void Register(string key, Rect rect, bool cover)
        {
            byKey[key] = rect;
            if (cover) coverKeys.Add(key);
        }

        private static string LastSegment(string path)
        {
            int slash = path.LastIndexOf('/');
            return slash >= 0 ? path.Substring(slash + 1) : path;
        }

        private static string ParentSegment(string path)
        {
            int slash = path.LastIndexOf('/');
            if (slash <= 0) return null;
            int prev = path.LastIndexOf('/', slash - 1);
            return path.Substring(prev + 1, slash - prev - 1);
        }

        /// <summary>按 Resources 路径直接查标注（无兜底）。</summary>
        public static bool TryGetFaceByPath(string resourcePath, out Rect face)
        {
            EnsureLoaded();
            face = default;
            return !string.IsNullOrEmpty(resourcePath) && byKey.TryGetValue(resourcePath, out face);
        }

        /// <summary>查标注人脸框（无兜底）。memberId 可空；sprite 名唯一时只靠 sprite 也能查到。</summary>
        public static bool TryGetFace(string memberId, Sprite art, out Rect face)
            => TryGetFace(memberId, art, out face, out _);

        private static bool TryGetFace(string memberId, Sprite art, out Rect face, out bool cover)
        {
            EnsureLoaded();
            face = default;
            cover = false;
            string name = art != null ? art.name : null;
            if (string.IsNullOrEmpty(name)) return false;
            string key = null;
            if (!string.IsNullOrEmpty(memberId) && byKey.ContainsKey(memberId + "/" + name)) key = memberId + "/" + name;
            else if (byKey.ContainsKey(name)) key = name;
            else if (art.texture != null && byKey.ContainsKey(art.texture.name)) key = art.texture.name;
            if (key == null) return false;
            face = byKey[key];
            cover = coverKeys.Contains(key);
            return true;
        }

        /// <summary>该立绘是否为已构图好的头像（整张铺满即可）。</summary>
        public static bool IsCover(string memberId, Sprite art) => TryGetFace(memberId, art, out _, out bool cover) && cover;

        /// <summary>人脸框（归一化，y 自底向上）。无标注时按不透明区域顶部估算头部。</summary>
        public static Rect FaceFor(string memberId, Sprite art)
        {
            if (TryGetFace(memberId, art, out Rect face)) return face;
            return EstimateFace(art);
        }

        /// <summary>人脸框（Texture 版，按纹理名查；查不到用默认站姿估计）。</summary>
        public static Rect FaceFor(string memberId, Texture texture)
        {
            EnsureLoaded();
            if (texture != null)
            {
                if (!string.IsNullOrEmpty(memberId) && byKey.TryGetValue(memberId + "/" + texture.name, out Rect f)) return f;
                if (byKey.TryGetValue(texture.name, out f)) return f;
            }
            return DefaultFace(texture != null ? (float)texture.width / Mathf.Max(1, texture.height) : .5f);
        }

        /// <summary>SHARED-RULES 里约定的名字：等同 <see cref="UvFor(string, Sprite, float, Framing)"/>。</summary>
        public static Rect Crop(string memberId, Sprite art, float targetAspect, Framing framing = Framing.Head)
            => UvFor(memberId, art, targetAspect, framing);

        public static Rect Crop(Sprite art, float targetAspect, Framing framing = Framing.Head)
            => UvFor(null, art, targetAspect, framing);

        /// <summary>
        /// 裁切矩形（相对 sprite.rect 的归一化坐标，y 自底向上），宽高比 = targetAspect（宽/高，按像素）。
        /// 原图不够大时贴边平移，绝不越界。
        /// </summary>
        public static Rect UvFor(string memberId, Sprite art, float targetAspect, Framing framing = Framing.Head)
        {
            if (art == null) return new Rect(0f, 0f, 1f, 1f);
            if (!TryGetFace(memberId, art, out Rect face, out bool cover)) face = EstimateFace(art);
            return CropAround(face, art.rect.width, art.rect.height, targetAspect, framing, cover);
        }

        public static Rect UvFor(string memberId, Texture texture, float targetAspect, Framing framing = Framing.Head)
        {
            if (texture == null) return new Rect(0f, 0f, 1f, 1f);
            return CropAround(FaceFor(memberId, texture), texture.width, texture.height, targetAspect, framing);
        }

        /// <summary>纯数学：给定人脸框与原图尺寸，算构图裁切（测试可直接用）。</summary>
        public static Rect CropAround(Rect face, float srcW, float srcH, float targetAspect, Framing framing,
            bool cover = false)
        {
            if (srcW <= 0f || srcH <= 0f) return new Rect(0f, 0f, 1f, 1f);
            if (targetAspect <= 0f || float.IsNaN(targetAspect)) targetAspect = 1f;
            Params(framing, out float faceFrac, out float topGap);

            float faceH = Mathf.Max(face.height * srcH, 1f);
            float faceW = Mathf.Max(face.width * srcW, 1f);
            float ch = cover ? 1e9f : faceH / faceFrac;
            // 很宽的脸（侧脸/大发型）别让左右被切：宽度至少留脸宽的 1/0.7。
            float minW = faceW / (framing == Framing.Head ? .78f : .5f);
            if (ch * targetAspect < minW) ch = minW / targetAspect;
            float cw = ch * targetAspect;
            if (cw > srcW) { cw = srcW; ch = cw / targetAspect; }
            if (ch > srcH) { ch = srcH; cw = ch * targetAspect; }

            float cx = face.center.x * srcW;
            float x = Mathf.Clamp(cx - cw * .5f, 0f, srcW - cw);
            float top = face.yMax * srcH + ch * topGap;
            float y = Mathf.Clamp(top - ch, 0f, srcH - ch);
            return new Rect(x / srcW, y / srcH, cw / srcW, ch / srcH);
        }

        /// <summary>
        /// 显示框尺寸 → 默认构图：方/宽框 = 头像；高大框 = 半身；其余 = 胸像。
        /// </summary>
        public static Framing AutoFraming(Vector2 frameSize)
        {
            if (frameSize.x <= 0f || frameSize.y <= 0f) return Framing.Head;
            float aspect = frameSize.x / frameSize.y;
            if (aspect >= .8f) return Framing.Head;
            if (frameSize.y >= 420f && aspect < .62f) return Framing.Half;
            return Framing.Bust;
        }

        /// <summary>成员主立绘（catalog 的 PortraitResourcePath）的人脸框与原图尺寸。</summary>
        public static bool TryMemberFace(string memberId, out Rect face, out Vector2 size)
        {
            EnsureLoaded();
            face = default;
            size = default;
            string path = MemberPortraitPath(memberId);
            return path != null && byKey.TryGetValue(path, out face) && sizeByPath.TryGetValue(path, out size);
        }

        /// <summary>
        /// 成员主立绘上的构图矩形（归一化，y 自底向上）。PanelKit.MemberBustFocus 由此生成，
        /// 战斗特写 / 结算 / 选人弹窗等按 focus 摆图的旧代码自动跟随人脸数据。
        /// </summary>
        public static bool TryMemberFocus(string memberId, float pixelAspect, Framing framing, out Rect uv)
        {
            uv = default;
            if (!TryMemberFace(memberId, out Rect face, out Vector2 size)) return false;
            uv = CropAround(face, size.x, size.y, pixelAspect, framing);
            return true;
        }

        private static string MemberPortraitPath(string memberId)
        {
            if (string.IsNullOrEmpty(memberId)) return null;
            MemberDefinition[] members = GameModel.Members;
            if (members != null)
                for (int i = 0; i < members.Length; i++)
                    if (members[i] != null && members[i].Id == memberId && !string.IsNullOrEmpty(members[i].ResourcePath))
                        return members[i].ResourcePath;
            return null;
        }

        /// <summary>
        /// 缩略图（hero-*/thumb）放大后会糊：裁出来的像素少于显示像素时换成同构图的大图 portrait。
        /// </summary>
        public static Sprite Sharpest(string memberId, Sprite art, float displayHeightPx, float targetAspect, Framing framing)
        {
            if (art == null || string.IsNullOrEmpty(memberId) || art.name != "thumb") return art;
            Rect uv = UvFor(memberId, art, targetAspect, framing);
            if (uv.height * art.rect.height >= displayHeightPx) return art;
            Sprite big = Resources.Load<Sprite>("Art/Members/" + memberId + "/portrait");
            return big != null ? big : art;
        }

        /// <summary>
        /// 返回裁好的 Sprite（缓存）：直接塞给 Image 即可，不需要 Mask。
        /// targetAspect 取显示框的宽/高。
        /// </summary>
        public static Sprite CropSprite(string memberId, Sprite art, float targetAspect, Framing framing = Framing.Head)
        {
            if (art == null || art.texture == null) return art;
            Rect uv = UvFor(memberId, art, targetAspect, framing);
            string key = memberId + "|" + framing + "|" + Mathf.RoundToInt(targetAspect * 1000f);
            if (!cropCache.TryGetValue(art, out Dictionary<string, Sprite> perArt))
                cropCache[art] = perArt = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            if (perArt.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            Rect tr = art.textureRect;
            var px = new Rect(tr.x + uv.x * tr.width, tr.y + uv.y * tr.height, uv.width * tr.width, uv.height * tr.height);
            px.x = Mathf.Round(px.x); px.y = Mathf.Round(px.y);
            px.width = Mathf.Clamp(Mathf.Round(px.width), 1f, art.texture.width - px.x);
            px.height = Mathf.Clamp(Mathf.Round(px.height), 1f, art.texture.height - px.y);
            Sprite sprite = Sprite.Create(art.texture, px, new Vector2(.5f, .5f), art.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = art.name + "@" + framing;
            perArt[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// 在带 Mask/RectMask2D 的 frame 内摆放 image（image 以 frame 为父级）：不换 sprite，只放大位移，
        /// 让构图裁切铺满 frame。
        /// </summary>
        public static void Apply(Image image, RectTransform frame, string memberId, Framing framing = Framing.Head)
        {
            if (image == null || frame == null || image.sprite == null) return;
            float fw = frame.rect.width, fh = frame.rect.height;
            if (fw <= 0f || fh <= 0f) return;
            float pixels = fh * (image.canvas != null ? Mathf.Max(1f, image.canvas.scaleFactor) : 1f);
            image.sprite = Sharpest(memberId, image.sprite, pixels, fw / fh, framing);
            Rect src = image.sprite.rect;
            Rect uv = UvFor(memberId, image.sprite, fw / fh, framing);
            float scale = fw / (uv.width * src.width);
            image.preserveAspect = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(src.width * scale, src.height * scale);
            rect.anchoredPosition = new Vector2((.5f - uv.center.x) * src.width * scale,
                (.5f - uv.center.y) * src.height * scale);
        }

        /// <summary>设置 RawImage 的 texture + uvRect（按 RawImage 自身宽高比）。</summary>
        public static void Apply(RawImage raw, string memberId, Sprite art, Framing framing = Framing.Head)
        {
            if (raw == null || art == null) return;
            Rect r = raw.rectTransform.rect;
            float aspect = r.height > 0f ? r.width / r.height : 1f;
            Rect uv = UvFor(memberId, art, aspect, framing);
            Texture tex = art.texture;
            Rect tr = art.textureRect;
            raw.texture = tex;
            raw.uvRect = new Rect((tr.x + uv.x * tr.width) / tex.width, (tr.y + uv.y * tr.height) / tex.height,
                uv.width * tr.width / tex.width, uv.height * tr.height / tex.height);
        }

        public static void Apply(RawImage raw, string memberId, Texture texture, Framing framing = Framing.Head)
        {
            if (raw == null || texture == null) return;
            Rect r = raw.rectTransform.rect;
            float aspect = r.height > 0f ? r.width / r.height : 1f;
            raw.texture = texture;
            raw.uvRect = UvFor(memberId, texture, aspect, framing);
        }

        // ------------------------------------------------------------------ 兜底

        /// <summary>无标注：默认站姿立绘（头在上部居中）。</summary>
        private static Rect DefaultFace(float aspect)
        {
            // 竖图（全身）：头在上 8–20%；方图（头像）：脸在中上。
            if (aspect >= .8f) return new Rect(.30f, .38f, .40f, .38f);
            return new Rect(.40f, .80f, .20f, .11f);
        }

        /// <summary>按不透明区域顶部估算头部（读回一张小缩略图；无 GPU 时用默认值）。</summary>
        public static Rect EstimateFace(Sprite art)
        {
            if (art == null) return DefaultFace(.5f);
            if (estimated.TryGetValue(art, out Rect cached)) return cached;
            float aspect = art.rect.width / Mathf.Max(1f, art.rect.height);
            Rect result = DefaultFace(aspect);
            try
            {
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null && art.texture != null)
                    result = EstimateFromAlpha(art, aspect) ?? result;
            }
            catch (Exception) { }
            estimated[art] = result;
            return result;
        }

        private static Rect? EstimateFromAlpha(Sprite art, float aspect)
        {
            const int H = 96;
            int W = Mathf.Clamp(Mathf.RoundToInt(H * aspect), 8, 192);
            Texture tex = art.texture;
            Rect tr = art.textureRect;
            var scaleUv = new Vector2(tr.width / tex.width, tr.height / tex.height);
            var offsetUv = new Vector2(tr.x / tex.width, tr.y / tex.height);
            RenderTexture rt = RenderTexture.GetTemporary(W, H, 0, RenderTextureFormat.ARGB32);
            RenderTexture prev = RenderTexture.active;
            var read = new Texture2D(W, H, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(tex, rt, scaleUv, offsetUv);
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                read.Apply(false);
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }

            Color32[] px = read.GetPixels32();
            UnityEngine.Object.Destroy(read);
            int opaque = 0;
            for (int i = 0; i < px.Length; i++) if (px[i].a > 128) opaque++;
            if (opaque > px.Length * .97f) return null;   // 不透明背景图：无从估计

            // 自顶向下找第一行有足够不透明像素的行 = 头顶。
            int topRow = -1;
            for (int y = H - 1; y >= 0 && topRow < 0; y--)
            {
                int n = 0;
                for (int x = 0; x < W; x++) if (px[y * W + x].a > 128) n++;
                if (n >= Mathf.Max(2, W / 40)) topRow = y;
            }
            if (topRow < 0) return null;

            // 头部高度估计：竖图约占 13%，方图约 45%。
            float headH = aspect >= .8f ? .45f : .13f;
            float headTop = (topRow + 1f) / H;
            float headBottom = Mathf.Max(0f, headTop - headH);
            // 头部横向重心
            float sum = 0f, cnt = 0f;
            int y0 = Mathf.FloorToInt(headBottom * H), y1 = topRow;
            for (int y = y0; y <= y1; y++)
                for (int x = 0; x < W; x++)
                    if (px[y * W + x].a > 128) { sum += x; cnt++; }
            float cx = cnt > 0f ? (sum / cnt + .5f) / W : .5f;
            // 人脸框 ≈ 头部下 70%、宽为头高的 0.8（按像素换算）
            float faceH = headH * .7f;
            float faceW = Mathf.Min(.9f, faceH * .8f / aspect);
            return new Rect(Mathf.Clamp(cx - faceW * .5f, 0f, 1f - faceW), headBottom, faceW, faceH);
        }
    }
}
