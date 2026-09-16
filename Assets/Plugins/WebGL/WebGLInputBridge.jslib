// CHO-SIREN WebGL DOM input bridge for legacy uGUI InputField.
// Companion to Assets/Scripts/UI/WebGLInputBridge.cs (ChoSiren.WebGLInputBridge).
//
// While a uGUI InputField is being edited, a real transparent <input>/<textarea>
// is pinned over the field's on-screen rect and focused. The DOM element owns
// all keyboard/IME/clipboard interaction; committed text + selection are
// mirrored to Unity through Module.SendMessage -> OnWebSync, and Enter/Escape/
// blur are mirrored as end-edit signals. Intermediate IME composition text is
// never forwarded. The element is fully transparent (Unity renders the text,
// caret and selection natively) and is removed the instant editing ends, so no
// listeners or overlays outlive the edit session.
mergeInto(LibraryManager.library, {
  $CHO_WebGLInput: {
    el: null,            // live DOM element while attached, else null
    attached: false,     // true between Attach and Detach
    composing: false,    // inside an IME composition
    multiline: false,
    rect: { nx: 0, ny: 0, nw: 0, nh: 0, nfont: 0 },
    lastText: null,      // dedupe: last text sent to Unity (or pushed by it)
    lastSel: "",         // dedupe: last "start:end" selection sent to Unity
    handlers: null,      // bound element-level listeners
    docHandlers: null,   // bound document/window-level listeners

    canvas: function () {
      if (typeof Module !== "undefined" && Module && Module.canvas) {
        return Module.canvas;
      }
      return document.getElementById("unity-canvas") ||
             document.querySelector("canvas");
    },

    send: function (method, arg) {
      if (typeof Module !== "undefined" && Module && Module.SendMessage) {
        try { Module.SendMessage("WebGLInputBridge", method, arg); } catch (e) {}
      }
    },

    // One message carries selection + text: "selStart:selEnd:text".
    // Indices are UTF-16 code units on both sides, so they match C# string
    // indexing. Deduped so caret blinks and repeat events stay cheap.
    sendSync: function () {
      var B = CHO_WebGLInput, el = B.el;
      // Never forward intermediate IME composition text. compositionend
      // clears the flag before calling, so the committed text still goes.
      if (!el || !B.attached || B.composing) return;
      var s = el.selectionStart, e = el.selectionEnd, v = el.value;
      if (s == null || e == null) { s = e = v.length; }
      var sel = s + ":" + e;
      if (v === B.lastText && sel === B.lastSel) return;
      B.lastText = v;
      B.lastSel = sel;
      B.send("OnWebSync", sel + ":" + v);
    },

    // rect is normalized [0..1] in Unity screen space (y-up, bottom origin).
    // Map to CSS pixels through the canvas's live client rect so DPR, zoom
    // and page layout are handled by the browser.
    position: function () {
      var B = CHO_WebGLInput, el = B.el, c = B.canvas();
      if (!el || !c) return;
      var r = c.getBoundingClientRect();
      el.style.left = (r.left + B.rect.nx * r.width) + "px";
      el.style.top = (r.top + (1 - B.rect.ny - B.rect.nh) * r.height) + "px";
      var w = Math.max(2, B.rect.nw * r.width);
      var h = Math.max(2, B.rect.nh * r.height);
      el.style.width = w + "px";
      el.style.height = h + "px";
      el.style.fontSize = Math.max(1, B.rect.nfont * r.height) + "px";
      el.style.lineHeight = h + "px";
    },

    bind: function (el) {
      var B = CHO_WebGLInput;
      if (!B.handlers) {
        B.handlers = {
          input: function (ev) {
            if (!B.composing && !ev.isComposing) B.sendSync();
          },
          compstart: function () { B.composing = true; },
          compend: function () {
            B.composing = false;
            B.sendSync();
          },
          keydown: function (ev) {
            // While composing, Enter/Escape belong to the IME (commit/cancel
            // the candidate), never to the field.
            if (B.composing || ev.isComposing) return;
            // uGUI semantics: single-line always submits on Enter; multiline
            // submits on Enter only for MultiLineSubmit, and Shift+Enter
            // always stays a newline.
            if (ev.key === "Enter" &&
                (!B.multiline || (B.submitOnEnter && !ev.shiftKey))) {
              ev.preventDefault(); // no newline, no form navigation
              B.send("OnWebSubmit");
            } else if (ev.key === "Escape") {
              B.send("OnWebCancel");
            }
            // Everything else is handled natively by the DOM element; nothing
            // is forwarded to the canvas, so no duplicate key delivery.
          },
          blur: function () {
            if (B.attached) B.send("OnWebBlur");
          },
          // The browser delivers paste/copy/cut natively to the focused
          // element. Stop them bubbling to Unity's window-level handlers so
          // the text is applied exactly once (through the input event).
          clip: function (ev) { ev.stopPropagation(); }
        };
        B.docHandlers = {
          sel: function () {
            var el2 = B.el;
            if (B.attached && !B.composing && el2 && document.activeElement === el2 &&
                el2.selectionStart != null &&
                (el2.selectionStart + ":" + el2.selectionEnd) !== B.lastSel) {
              B.sendSync();
            }
          },
          pos: function () { B.position(); }
        };
      }
      el.addEventListener("input", B.handlers.input);
      el.addEventListener("compositionstart", B.handlers.compstart);
      el.addEventListener("compositionend", B.handlers.compend);
      el.addEventListener("keydown", B.handlers.keydown);
      el.addEventListener("blur", B.handlers.blur);
      el.addEventListener("paste", B.handlers.clip);
      el.addEventListener("copy", B.handlers.clip);
      el.addEventListener("cut", B.handlers.clip);
      document.addEventListener("selectionchange", B.docHandlers.sel);
      window.addEventListener("resize", B.docHandlers.pos);
      window.addEventListener("scroll", B.docHandlers.pos, true);
      if (window.visualViewport) {
        window.visualViewport.addEventListener("resize", B.docHandlers.pos);
        window.visualViewport.addEventListener("scroll", B.docHandlers.pos);
      }
    },

    unbind: function (el) {
      var B = CHO_WebGLInput;
      if (!B.handlers) return;
      el.removeEventListener("input", B.handlers.input);
      el.removeEventListener("compositionstart", B.handlers.compstart);
      el.removeEventListener("compositionend", B.handlers.compend);
      el.removeEventListener("keydown", B.handlers.keydown);
      el.removeEventListener("blur", B.handlers.blur);
      el.removeEventListener("paste", B.handlers.clip);
      el.removeEventListener("copy", B.handlers.clip);
      el.removeEventListener("cut", B.handlers.clip);
      document.removeEventListener("selectionchange", B.docHandlers.sel);
      window.removeEventListener("resize", B.docHandlers.pos);
      window.removeEventListener("scroll", B.docHandlers.pos, true);
      if (window.visualViewport) {
        window.visualViewport.removeEventListener("resize", B.docHandlers.pos);
        window.visualViewport.removeEventListener("scroll", B.docHandlers.pos);
      }
    }
  },

  // flags: bit0 multiline, bit1 password, bit2 readonly, bit3 submitOnEnter
  // (uGUI MultiLineSubmit), bits4-5 h-align (0 left, 1 center, 2 right).
  WebGLInputBridge_Attach: function (nx, ny, nw, nh, nfont, flags,
                                     textPtr, selStart, selEnd) {
    var B = CHO_WebGLInput;
    var text = UTF8ToString(textPtr);
    var multiline = (flags & 1) !== 0;
    var password = (flags & 2) !== 0;
    var readOnly = (flags & 4) !== 0;
    var submitOnEnter = (flags & 8) !== 0;
    var align = (flags >> 4) & 3;

    if (B.el) {
      // Re-attach: tear the previous element down silently first.
      B.attached = false;
      B.unbind(B.el);
      if (B.el.parentNode) B.el.parentNode.removeChild(B.el);
      B.el = null;
    }

    var el = document.createElement(multiline ? "textarea" : "input");
    if (!multiline) el.type = password ? "password" : "text";
    el.readOnly = readOnly;
    el.setAttribute("autocomplete", "off");
    el.setAttribute("autocorrect", "off");
    el.setAttribute("autocapitalize", "off");
    el.setAttribute("spellcheck", "false");
    el.setAttribute("aria-label", "game text input");
    var s = el.style;
    s.position = "fixed";
    s.margin = "0";
    s.padding = "0";
    s.border = "0";
    s.outline = "0";
    s.borderRadius = "0";
    s.boxShadow = "none";
    s.background = "transparent";
    // Unity keeps rendering text/caret/selection itself; the element only
    // needs to exist as a real editable target (the IME anchor).
    s.color = "transparent";
    s.caretColor = "transparent";
    s.resize = "none";
    s.overflow = "hidden";
    s.whiteSpace = multiline ? "pre-wrap" : "pre";
    s.textAlign = align === 1 ? "center" : align === 2 ? "right" : "left";
    s.fontFamily = "sans-serif";
    s.webkitAppearance = "none";
    s.appearance = "none";
    s.zIndex = "2147483647";

    B.rect = { nx: nx, ny: ny, nw: nw, nh: nh, nfont: nfont };
    B.multiline = multiline;
    B.submitOnEnter = submitOnEnter;
    B.el = el;
    B.composing = false;
    B.lastText = text;
    B.lastSel = selStart + ":" + selEnd;

    document.body.appendChild(el);
    el.value = text;
    B.position();
    B.bind(el);
    B.attached = true;
    try { el.setSelectionRange(selStart, selEnd); } catch (e) {}
    try { el.focus({ preventScroll: true }); } catch (e) { el.focus(); }
    // If the element could not take focus (e.g. unfocused page) the DOM path
    // is dead — end the edit honestly instead of leaving a stale overlay.
    if (document.activeElement !== el) {
      B.send("OnWebBlur");
    }
  },
  WebGLInputBridge_Attach__deps: ["$CHO_WebGLInput"],

  WebGLInputBridge_Detach: function () {
    var B = CHO_WebGLInput;
    if (!B.el) return;
    B.attached = false;   // blur handler checks this before messaging Unity
    B.composing = false;
    var el = B.el;
    B.el = null;
    B.unbind(el);
    try { el.blur(); } catch (e) {}
    if (el.parentNode) el.parentNode.removeChild(el);
    // Give keyboard focus back to the game canvas when nothing else claimed
    // it — otherwise game hotkeys stay dead after an Enter/Escape-completed
    // edit. If the blur moved focus to another real page element, leave it.
    try {
      if (document.activeElement === document.body ||
          document.activeElement === document.documentElement) {
        var c = B.canvas();
        if (c && c.focus) c.focus({ preventScroll: true });
      }
    } catch (e) {}
  },
  WebGLInputBridge_Detach__deps: ["$CHO_WebGLInput"],

  WebGLInputBridge_SetRect: function (nx, ny, nw, nh, nfont) {
    var B = CHO_WebGLInput;
    B.rect = { nx: nx, ny: ny, nw: nw, nh: nh, nfont: nfont };
    B.position();
  },
  WebGLInputBridge_SetRect__deps: ["$CHO_WebGLInput"],

  // Unity changed the text itself (reset/clear/validation): push it back.
  WebGLInputBridge_SetText: function (textPtr, selStart, selEnd) {
    var B = CHO_WebGLInput;
    if (!B.el) return;
    var v = UTF8ToString(textPtr);
    if (B.el.value !== v) B.el.value = v;
    try { B.el.setSelectionRange(selStart, selEnd); } catch (e) {}
    B.lastText = v;
    B.lastSel = selStart + ":" + selEnd;
  },
  WebGLInputBridge_SetText__deps: ["$CHO_WebGLInput"],

  WebGLInputBridge_IsAttached: function () {
    return (CHO_WebGLInput.el && CHO_WebGLInput.attached) ? 1 : 0;
  },
  WebGLInputBridge_IsAttached__deps: ["$CHO_WebGLInput"]
});
