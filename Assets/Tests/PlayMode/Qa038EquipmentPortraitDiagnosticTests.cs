using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// Temporary diagnostic for the equipment-page portrait rendering white in the
    /// WebGL build. Dumps EquipmentPortrait/overlay state after the accessory page opens.
    /// </summary>
    public class Qa038EquipmentPortraitDiagnosticTests
    {
        [UnityTest]
        public IEnumerator DumpEquipmentPortraitState()
        {
            ChoSirenApp app = new GameObject("diag").AddComponent<ChoSirenApp>();
            yield return null;
            GameObject nav = GameObject.Find("Nav-accessory");
            Assert.That(nav, Is.Not.Null);
            nav.GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;

            GameObject portrait = GameObject.Find("EquipmentPortrait");
            Debug.Log("DIAG portrait exists: " + (portrait != null));
            if (portrait != null)
            {
                Image image = portrait.GetComponent<Image>();
                RectTransform rect = portrait.GetComponent<RectTransform>();
                Debug.Log($"DIAG sprite={(image.sprite == null ? "null" : image.sprite.name)} " +
                          $"tex={(image.sprite == null ? "null" : image.sprite.texture.name)} " +
                          $"color={image.color} type={image.type} enabled={image.enabled} " +
                          $"size={rect.rect.size} anchored={rect.anchoredPosition} pivot={rect.pivot} " +
                          $"anchorMin={rect.anchorMin} scale={rect.localScale}");
                Debug.Log($"DIAG inheritedAlpha={image.canvasRenderer.GetInheritedAlpha()} " +
                          $"hasFade={(portrait.GetComponent<EquipmentPolish038PortraitFade>() != null)} " +
                          $"mainTex={(image.mainTexture == null ? "null" : image.mainTexture.name)} " +
                          $"parent={portrait.transform.parent.name}");
                Vector3[] corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Debug.Log($"DIAG worldCorners BL={corners[0]} TR={corners[2]}");
            }
            GameObject aperture = GameObject.Find("EquipmentPolish038Aperture");
            if (aperture != null)
            {
                Image ai = aperture.GetComponent<Image>();
                Debug.Log($"DIAG aperture sprite={(ai.sprite == null ? "null" : ai.sprite.name)} color={ai.color}");
            }
            GameObject plaque = GameObject.Find("EquipmentMemberPlaque");
            if (plaque != null)
            {
                Image pi = plaque.GetComponent<Image>();
                Debug.Log($"DIAG plaque sprite={(pi.sprite == null ? "null" : pi.sprite.name)} color={pi.color}");
            }
            Object.Destroy(app.gameObject);
        }

        [UnityTest]
        public IEnumerator CaptureHudScreenshot()
        {
            if (Application.isBatchMode)
                Assert.Ignore("Framebuffer capture needs a real drawing frame; run interactively.");
            ChoSirenApp app = new GameObject("diag").AddComponent<ChoSirenApp>();
            yield return null;
            yield return null;

            Canvas canvas = Object.FindObjectOfType<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            GameObject camObj = new GameObject("DiagCam");
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;
            yield return null;
            Canvas.ForceUpdateCanvases();

            RenderTexture rt = new RenderTexture(720, 1536, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D shot = new Texture2D(720, 1536, TextureFormat.RGBA32, false);
            shot.ReadPixels(new Rect(0, 0, 720, 1536), 0, 0);
            shot.Apply();
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes("/tmp/diag-hud.png", shot.EncodeToPNG());

            GameObject nav = GameObject.Find("Nav-members");
            if (nav != null)
            {
                nav.GetComponent<Button>().onClick.Invoke();
                yield return null;
                Canvas.ForceUpdateCanvases();
                cam.Render();
                shot.ReadPixels(new Rect(0, 0, 720, 1536), 0, 0);
                shot.Apply();
                System.IO.File.WriteAllBytes("/tmp/diag-hud-members.png", shot.EncodeToPNG());
            }
            cam.targetTexture = null;
            rt.Release();
            Object.Destroy(camObj);
            Object.Destroy(app.gameObject);
        }

        [UnityTest]
        public IEnumerator StaminaTextWrapProbe()
        {
            ChoSirenApp app = new GameObject("diag").AddComponent<ChoSirenApp>();
            yield return null;
            GameObject stamina = GameObject.Find("Currency-stamina");
            Assert.That(stamina, Is.Not.Null);
            Text value = stamina.GetComponentInChildren<Text>();
            value.text = "120/120";
            value.horizontalOverflow = HorizontalWrapMode.Wrap;
            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = 10;
            value.resizeTextMaxSize = 17;
            value.rectTransform.sizeDelta = new Vector2(44, 26);
            yield return null;
            Canvas.ForceUpdateCanvases();
            TextGenerator gen = value.cachedTextGenerator;
            Debug.Log($"DIAG wrap probe: lines={gen.lineCount} bestFitSize={gen.fontSizeUsedForBestFit} " +
                      $"verts={gen.vertexCount} chars={gen.characterCountVisible}/{gen.characterCount} renderedSize={value.fontSize}");
            Debug.Log($"DIAG wrap probe2: preferredW={value.preferredWidth}");
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            yield return null;
            Canvas.ForceUpdateCanvases();
            Debug.Log($"DIAG overflow probe: bestFitSize={value.cachedTextGenerator.fontSizeUsedForBestFit} preferredW={value.preferredWidth}");
            Object.Destroy(app.gameObject);
        }

        [UnityTest]
        public IEnumerator DumpHudNameplateState()
        {
            ChoSirenApp app = new GameObject("diag").AddComponent<ChoSirenApp>();
            yield return null;
            yield return null;

            foreach (string nodeName in new[] { "PlayerName", "TeamAverageLevel", "PlayerLevel", "AvatarMask", "Avatar" })
            {
                GameObject node = GameObject.Find(nodeName);
                Debug.Log($"DIAG HUD {nodeName}: exists={node != null}");
                if (node == null) continue;
                RectTransform rect = node.GetComponent<RectTransform>();
                Vector3[] corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Text text = node.GetComponent<Text>();
                Image image = node.GetComponent<Image>();
                string extra = text != null
                    ? $"text='{text.text}' font={(text.font == null ? "null" : text.font.name)} color={text.color} enabled={text.enabled} bestFit={text.resizeTextForBestFit}"
                    : image != null
                        ? $"sprite={(image.sprite == null ? "null" : image.sprite.name)} color={image.color} enabled={image.enabled}"
                        : "no graphic";
                if (text != null)
                {
                    Canvas.ForceUpdateCanvases();
                    TextGenerator gen = text.cachedTextGenerator;
                    Debug.Log($"DIAG HUD {nodeName}: verts={gen.vertexCount} chars={gen.characterCountVisible}/{gen.characterCount} " +
                              $"lines={gen.lineCount} fontSizeUsed={text.cachedTextGeneratorForLayout} " +
                              $"prefW={text.preferredWidth} prefH={text.preferredHeight} " +
                              $"vOverflow={text.verticalOverflow} hOverflow={text.horizontalOverflow}");
                }
                Graphic g = node.GetComponent<Graphic>();
                string cull = g == null ? "n/a" : $"cull={g.canvasRenderer.cull} hasPopInstruction={g.canvasRenderer.hasPopInstruction} popCount={g.canvasRenderer.popMaterialCount}";
                Debug.Log($"DIAG HUD {nodeName}: {extra} rect={rect.rect.size} anchored={rect.anchoredPosition} " +
                          $"corners BL={corners[0]} TR={corners[2]} alpha={(g == null ? -1f : g.canvasRenderer.GetInheritedAlpha())} " +
                          $"active={node.activeInHierarchy} parent={node.transform.parent.name} {cull} sibIdx={node.transform.GetSiblingIndex()}");
            }
            Object.Destroy(app.gameObject);
        }

        [UnityTest]
        public IEnumerator DumpLobbyFootprintRegions()
        {
            ChoSirenApp app = new GameObject("diag").AddComponent<ChoSirenApp>();
            yield return null;
            yield return null;
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.GetComponent<CanvasScaler>().enabled = false;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.anchorMin = canvasRect.anchorMax = new Vector2(.5f, .5f);
            canvasRect.pivot = new Vector2(.5f, .5f);
            canvasRect.sizeDelta = new Vector2(720, 1536);
            canvasRect.localScale = Vector3.one;
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return null;

            Image board = GameObject.Find("LobbyHomeGolden038")?.GetComponent<Image>();
            if (board == null)
                foreach (Image im in Object.FindObjectsByType<Image>(FindObjectsSortMode.None))
                    if (im.sprite != null && im.sprite.name.Contains("lobby")) { board = im; break; }
            Debug.Log($"DIAG FOOT board={(board == null ? "none" : board.name)} sprite={(board?.sprite?.name)}");
            if (board != null)
            {
                RectTransform boardRect = board.rectTransform;
                Sprite sprite = board.sprite;
                Rect boardLocal = boardRect.rect;
                float fit = Mathf.Min(boardLocal.width / sprite.rect.width,
                    boardLocal.height / sprite.rect.height);
                Vector2 boardCentre = new Vector2(boardLocal.width * (.5f - boardRect.pivot.x),
                    boardLocal.height * (.5f - boardRect.pivot.y));
                Debug.Log($"DIAG FOOT boardRect={boardRect.rect} lossy={boardRect.lossyScale} pivot={boardRect.pivot}");
                Vector3[] bc = new Vector3[4];
                boardRect.GetWorldCorners(bc);
                Debug.Log($"DIAG FOOT boardWorld bl={bc[0]} tr={bc[2]}");
                Transform cards = boardRect.parent?.Find("LobbyCards") ?? boardRect.root.Find("LobbyCards");
                if (cards == null)
                    foreach (RectTransform rt in Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
                        if (rt.name == "LobbyCards") { cards = rt; break; }
                Debug.Log($"DIAG FOOT cardLayer={(cards == null ? "none" : cards.GetComponent<RectTransform>().rect.ToString())}");
                foreach (string name in new[] { "PracticeRoom", "AlbumProduction", "Tasks", "LiveOnStage" })
                {
                    GameObject node = GameObject.Find(name);
                    RectTransform host = node == null ? null : node.GetComponent<RectTransform>();
                    if (host == null) { Debug.Log($"DIAG FOOT {name}: missing"); continue; }
                    Vector3[] corners = new Vector3[4];
                    host.GetWorldCorners(corners);
                    Debug.Log($"DIAG FOOT {name}: hostRect={host.rect} anchored={host.anchoredPosition} " +
                              $"worldBL={corners[0]} worldTR={corners[2]}");
                    float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                    foreach (Vector3 wc in corners)
                    {
                        Vector3 local = boardRect.InverseTransformPoint(wc);
                        float px = (local.x - boardCentre.x) / fit + sprite.rect.width * .5f;
                        float py = sprite.rect.height * .5f - (local.y - boardCentre.y) / fit;
                        minX = Mathf.Min(minX, px); maxX = Mathf.Max(maxX, px);
                        minY = Mathf.Min(minY, py); maxY = Mathf.Max(maxY, py);
                    }
                    Debug.Log($"DIAG FOOT {name}: board region x={minX:F1} y={minY:F1} w={maxX - minX:F1} h={maxY - minY:F1}");
                }
            }
            Object.Destroy(app.gameObject);
        }
    }
}
