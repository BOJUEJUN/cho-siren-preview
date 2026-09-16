using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace ChoSiren
{
    /// <summary>
    /// WebGL-only bridge that gives legacy uGUI InputFields a real DOM text
    /// element while they are being edited, so desktop IME composition (e.g.
    /// Chinese member names), OS clipboard paste, and caret/selection behave
    /// natively in the browser. Committed text and selection are mirrored back
    /// through the InputField's own text/onValueChanged/onEndEdit flow; nothing
    /// reaches the field until an IME composition commits. Self-installs at
    /// startup (no scene or per-field changes needed) and cleans itself up when
    /// the field is deselected, disabled, or destroyed. Outside WebGL — and on
    /// mobile, where Unity already opens a native keyboard — it stays inert.
    /// </summary>
    public sealed class WebGLInputBridge : MonoBehaviour
    {
        private const int FlagMultiline = 1;
        private const int FlagPassword = 2;
        private const int FlagReadOnly = 4;
        private const int FlagSubmitOnEnter = 8;
        private const int AlignShift = 4;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void WebGLInputBridge_Attach(
            float nx, float ny, float nw, float nh, float nfont,
            int flags, string text, int selStart, int selEnd);
        [DllImport("__Internal")]
        private static extern void WebGLInputBridge_Detach();
        [DllImport("__Internal")]
        private static extern void WebGLInputBridge_SetRect(
            float nx, float ny, float nw, float nh, float nfont);
        [DllImport("__Internal")]
        private static extern void WebGLInputBridge_SetText(
            string text, int selStart, int selEnd);
        [DllImport("__Internal")]
        private static extern int WebGLInputBridge_IsAttached();
#else
        private static void WebGLInputBridge_Attach(float nx, float ny, float nw, float nh,
            float nfont, int flags, string text, int selStart, int selEnd) { }
        private static void WebGLInputBridge_Detach() { }
        private static void WebGLInputBridge_SetRect(float nx, float ny, float nw,
            float nh, float nfont) { }
        private static void WebGLInputBridge_SetText(string text, int selStart, int selEnd) { }
        private static int WebGLInputBridge_IsAttached() { return 0; }
#endif

        /// <summary>True while a live DOM editing element is attached (diagnostics/tests).</summary>
        public static bool DomElementActive =>
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInputBridge_IsAttached() != 0;
#else
            false;
#endif

        private static readonly Vector3[] s_Corners = new Vector3[4];

        private InputField _field;
        private bool _attached;
        private string _lastText = string.Empty;
        private Rect _lastRect;
        private float _lastFont = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindAnyObjectByType<WebGLInputBridge>() != null) return;
            GameObject go = new GameObject(nameof(WebGLInputBridge));
            DontDestroyOnLoad(go);
            go.AddComponent<WebGLInputBridge>();
        }

        private void LateUpdate()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Tick();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private void Tick()
        {
            // Mobile WebGL keeps Unity's native TouchScreenKeyboard overlay path.
            if (Application.isMobilePlatform)
            {
                if (_attached) DetachField();
                _field = null;
                return;
            }

            EventSystem events = EventSystem.current;
            GameObject selected = events != null ? events.currentSelectedGameObject : null;
            InputField field = selected != null ? selected.GetComponent<InputField>() : null;
            if (field != null && (!field.isFocused || !field.interactable || !field.isActiveAndEnabled))
                field = null;

            if (field != _field)
            {
                if (_attached) DetachField();
                _field = field;
                if (_field != null) AttachField();
            }

            if (!_attached) return;

            // Covers the field being hidden/disabled/destroyed or deselected by
            // game code mid-edit — never leaves an orphaned DOM element behind.
            if (_field == null || !_field.isFocused || !_field.interactable ||
                !_field.isActiveAndEnabled || selected != _field.gameObject)
            {
                DetachField();
                _field = null;
                return;
            }

            Rect rect = NormalizedScreenRect((RectTransform)_field.transform);
            float font = NormalizedFontHeight(_field, rect);
            if (rect != _lastRect || !Mathf.Approximately(font, _lastFont))
            {
                _lastRect = rect;
                _lastFont = font;
                WebGLInputBridge_SetRect(rect.x, rect.y, rect.width, rect.height, font);
            }

            // External text changes (programmatic clear/reset) are pushed back
            // to the DOM element; text we mirrored ourselves is skipped.
            if (_field.text != _lastText)
            {
                _lastText = _field.text;
                int caret = Mathf.Clamp(_field.caretPosition, 0, _lastText.Length);
                WebGLInputBridge_SetText(_lastText, caret, caret);
            }
        }
#else
        private void Tick() { }
#endif

        private void AttachField()
        {
            _lastText = _field.text ?? string.Empty;
            Rect rect = NormalizedScreenRect((RectTransform)_field.transform);
            _lastRect = rect;
            _lastFont = NormalizedFontHeight(_field, rect);
            int anchor = Mathf.Clamp(_field.selectionAnchorPosition, 0, _lastText.Length);
            int focus = Mathf.Clamp(_field.selectionFocusPosition, 0, _lastText.Length);
            WebGLInputBridge_Attach(rect.x, rect.y, rect.width, rect.height, _lastFont,
                FlagsFor(_field), _lastText, Mathf.Min(anchor, focus), Mathf.Max(anchor, focus));
            _attached = true;
        }

        private void DetachField()
        {
            _attached = false;
            WebGLInputBridge_Detach();
        }

        private void OnDestroy()
        {
            // If the bridge object itself is torn down mid-edit, the DOM
            // element and its listeners must not leak.
            if (_attached)
            {
                _attached = false;
                WebGLInputBridge_Detach();
            }
        }

        // ---- JS -> Unity callbacks (invoked via Module.SendMessage) ----

        /// <summary>Payload: "{selectionStart}:{selectionEnd}:{text}".</summary>
        public void OnWebSync(string payload)
        {
            if (_field == null || string.IsNullOrEmpty(payload)) return;
            int i = payload.IndexOf(':');
            if (i < 0) return;
            int j = payload.IndexOf(':', i + 1);
            if (j < 0) return;
            int selStart, selEnd;
            if (!int.TryParse(payload.Substring(0, i), out selStart)) return;
            if (!int.TryParse(payload.Substring(i + 1, j - i - 1), out selEnd)) return;
            string text = payload.Substring(j + 1);

            if (text != _field.text)
            {
                // Native path: character validation, characterLimit truncation
                // and onValueChanged all run inside the text setter.
                _field.text = text;
                if (_field == null) return; // an onValueChanged listener rebuilt the screen
            }
            _lastText = _field.text;

            int len = _field.text.Length;
            _field.selectionAnchorPosition = Mathf.Clamp(selStart, 0, len);
            _field.selectionFocusPosition = Mathf.Clamp(selEnd, 0, len);

            // Unity-side filtering may have shortened the text (characterLimit);
            // push the effective value back so the DOM element stays honest.
            if (_field.text != text)
            {
                int caret = Mathf.Clamp(_field.caretPosition, 0, len);
                WebGLInputBridge_SetText(_field.text, caret, caret);
            }
        }

        /// <summary>Enter on a single-line field: end editing natively.</summary>
        public void OnWebSubmit()
        {
            EndEdit();
        }

        /// <summary>Escape: end editing the same way a click-out does.</summary>
        public void OnWebCancel()
        {
            EndEdit();
        }

        /// <summary>DOM element lost focus (click outside, Tab, window blur).</summary>
        public void OnWebBlur()
        {
            EndEdit();
        }

        private void EndEdit()
        {
            InputField field = _field;
            EventSystem events = EventSystem.current;
            if (field != null && events != null &&
                events.currentSelectedGameObject == field.gameObject)
            {
                // Deselect runs InputField.OnDeselect -> DeactivateInputField,
                // which invokes onEndEdit exactly once — the same path a native
                // Enter submit or click-out takes.
                events.SetSelectedGameObject(null);
            }
            _field = null;
            if (_attached)
            {
                _attached = false;
                WebGLInputBridge_Detach();
            }
        }

        private static Rect NormalizedScreenRect(RectTransform rt)
        {
            rt.GetWorldCorners(s_Corners);
            Camera cam = null;
            Canvas canvas = rt.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = canvas.worldCamera;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, s_Corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, s_Corners[2]);
            Vector2 min = Vector2.Min(a, b);
            Vector2 max = Vector2.Max(a, b);
            float sw = Mathf.Max(1f, Screen.width);
            float sh = Mathf.Max(1f, Screen.height);
            return new Rect(min.x / sw, min.y / sh,
                (max.x - min.x) / sw, (max.y - min.y) / sh);
        }

        private static float NormalizedFontHeight(InputField field, Rect normalizedRect)
        {
            Text label = field.textComponent;
            RectTransform rt = (RectTransform)field.transform;
            if (label == null || label.fontSize <= 0 || rt.rect.height < 0.001f)
                return normalizedRect.height * 0.6f;
            return label.fontSize * normalizedRect.height / rt.rect.height;
        }

        private static int FlagsFor(InputField field)
        {
            int flags = 0;
            if (field.lineType != InputField.LineType.SingleLine) flags |= FlagMultiline;
            if (field.lineType == InputField.LineType.MultiLineSubmit) flags |= FlagSubmitOnEnter;
            if (field.inputType == InputField.InputType.Password ||
                field.contentType == InputField.ContentType.Password) flags |= FlagPassword;
            if (field.readOnly) flags |= FlagReadOnly;
            Text label = field.textComponent;
            if (label != null)
            {
                switch (label.alignment)
                {
                    case TextAnchor.UpperCenter:
                    case TextAnchor.MiddleCenter:
                    case TextAnchor.LowerCenter:
                        flags |= 1 << AlignShift;
                        break;
                    case TextAnchor.UpperRight:
                    case TextAnchor.MiddleRight:
                    case TextAnchor.LowerRight:
                        flags |= 2 << AlignShift;
                        break;
                }
            }
            return flags;
        }
    }
}
