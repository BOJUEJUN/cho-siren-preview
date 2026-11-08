"use strict";
// 2026-11-07: composite the runners clip (left half colour, right half alpha) into the loading canvas.
(function () {
  const video = document.getElementById("ld-runners-src");
  const canvas = document.getElementById("ld-runners");
  const loadingScreen = document.getElementById("loading");
  if (!video || !canvas || !canvas.getContext) return;
  const ctx = canvas.getContext("2d", { willReadFrequently: true });
  const stage = document.createElement("canvas");
  stage.width = 960; stage.height = 154;
  const work = stage.getContext("2d", { willReadFrequently: true });
  const reduce = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  function frame() {
    if (loadingScreen && (loadingScreen.classList.contains("is-hidden") || loadingScreen.classList.contains("is-ready"))) { video.pause(); return; }
    if (video.readyState >= 2 && video.videoWidth) {
      const rowTop = Math.round(video.videoHeight * 84 / 270), rows = Math.round(video.videoHeight * 154 / 270);
      work.drawImage(video, 0, rowTop, video.videoWidth, rows, 0, 0, 960, 154);
      const colour = work.getImageData(0, 0, 480, 154), alpha = work.getImageData(480, 0, 480, 154);
      const c = colour.data, a = alpha.data;
      for (let i = 3; i < c.length; i += 4) c[i] = a[i - 3];
      ctx.putImageData(colour, 0, 0);
    }
    if (!reduce) requestAnimationFrame(frame);
  }
  const start = () => { const p = video.play && video.play(); if (p && p.catch) p.catch(() => {}); requestAnimationFrame(frame); };
  if (video.readyState >= 2) start(); else video.addEventListener("loadeddata", start, { once: true });
})();
