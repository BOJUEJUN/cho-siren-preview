using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    // Stream only the selected stage's art. Twenty scenic images never inflate the startup archive.
    [RequireComponent(typeof(RawImage))]
    public sealed class StageSceneArt1102 : MonoBehaviour
    {
        RawImage image;
        Texture2D owned;
        UnityWebRequest request;
        Coroutine loading;
        string selected;
        public string Selected => selected;
        void Awake()
        {
            image = GetComponent<RawImage>();
            image.color = new Color32(16, 9, 32, 255);
        }
        public void Show(string stageId)
        {
            if (stageId == selected) return;
            selected = stageId;
            if (loading != null) StopCoroutine(loading);
            request?.Abort(); request?.Dispose(); request = null;
            if (image == null) image = GetComponent<RawImage>();
            loading = StartCoroutine(Load(stageId));
        }
        /// <summary>
        /// 2026-11-11 (每章 20 关): x-11…x-20 have no scenic art of their own yet; they show x-1…x-10's scene mirrored,
        /// so neighbouring stages never look identical.
        /// </summary>
        internal static string SceneFile(string stage, out bool mirrored)
        {
            mirrored = false;
            string[] parts = (stage ?? string.Empty).Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out int n) && n > 10 && n <= 20)
            {
                mirrored = true;
                return $"{parts[0]}-{parts[1]}-{n - 10}";
            }
            return stage;
        }

        bool mirrored;

        IEnumerator Load(string stage)
        {
            string file = SceneFile(stage, out mirrored);
            string url = Application.streamingAssetsPath.TrimEnd('/') + "/LevelScenes1102/" + file + ".jpg";
            if (!url.Contains("://")) url = new Uri(System.IO.Path.GetFullPath(url)).AbsoluteUri;
            request = UnityWebRequestTexture.GetTexture(url, true); request.timeout = 15;
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success && stage == selected)
            {
                Texture2D next = DownloadHandlerTexture.GetContent(request);
                image.texture = next; image.color = Color.white;
                if (owned != null) Destroy(owned); owned = next;
                Fit();
            }
            else Debug.LogWarning("Level scene unavailable: " + stage);
            request.Dispose(); request = null; loading = null;
        }
        void LateUpdate() => Fit();
        void Fit()
        {
            if (owned == null || image == null || image.rectTransform.rect.height <= 0) return;
            float target = image.rectTransform.rect.width / image.rectTransform.rect.height;
            float source = owned.width / (float)owned.height;
            float x = target < source ? target / source : 1f;
            float y = target > source ? source / target : 1f;
            image.uvRect = mirrored ? new Rect((1 + x) * .5f, (1 - y) * .5f, -x, y) : new Rect((1 - x) * .5f, (1 - y) * .5f, x, y);
        }
        void OnDestroy() { request?.Abort(); request?.Dispose(); if (owned != null) Destroy(owned); }
    }
}
