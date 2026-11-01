// Shared code for the Fx1023 battle VFX shaders (procedural cel-shaded light effects, ported from the
// web preview). UI-compatible: stencil/RectMask2D clip like UI/Default; colours are premultiplied, and the
// material's blend factors decide between alpha (One, OneMinusSrcAlpha) and additive (One, One).
#ifndef FX1023_INCLUDED
#define FX1023_INCLUDED
#include "UnityCG.cginc"
#include "UnityUI.cginc"

struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 world : TEXCOORD1; };

sampler2D _MainTex, _Mask;
float4 _ClipRect;
float _T, _FxTime, _Alpha, _Seed, _Clip;
float4 _Deep, _Main, _Hot, _Light, _Acc, _Ink;

v2f vert(appdata_t v)
{
    v2f o;
    o.world = v.vertex;
    o.pos = UnityObjectToClipPos(v.vertex);
    o.uv = float2(v.texcoord.x, 1. - v.texcoord.y);      // y = 0 at the top, as in the web shaders
    o.color = v.color;
    return o;
}

float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
float noise(float2 p)
{
    float2 i = floor(p), f = frac(p); f = f * f * (3. - 2. * f);
    return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
}
float fbm(float2 p)
{
    float s = 0., a = .5;
    for (int k = 0; k < 4; k++) { s += a * noise(p); p = p * 2.03 + float2(1.7, 9.2); a *= .5; }
    return s;
}
// GLSL-style mod (always positive for a positive divisor)
float modf_(float x, float y) { return x - y * floor(x / y); }
// a hard edge kept one pixel soft at any zoom: what keeps the cel bands crisp
float aastep(float th, float x) { float w = max(fwidth(x) * .75, 1e-4); return smoothstep(th - w, th + w, x); }
float dot2(float2 v) { return dot(v, v); }
float sdHeart(float2 p)
{
    p.x = abs(p.x);
    if (p.y + p.x > 1.) return sqrt(dot2(p - float2(.25, .75))) - sqrt(2.) / 4.;
    return sqrt(min(dot2(p - float2(0., 1.)), dot2(p - .5 * max(p.x + p.y, 0.)))) * sign(p.x - p.y);
}
float2 rot2(float2 p, float a) { float c = cos(a), s = sin(a); return float2(c * p.x - s * p.y, s * p.x + c * p.y); }
float maskAt(float2 uv) { return any(uv < 0.) || any(uv > 1.) ? 0. : tex2D(_Mask, uv).r; }
// _Clip: 0 whole quad, 1 only the far (upper) half, 2 only the near (lower) half — for rings round the enemy
bool clipped(float2 uv) { return (_Clip == 1. && uv.y > .5) || (_Clip == 2. && uv.y < .5); }
float band(float r, float r0, float r1) { return aastep(r0, r) * (1. - aastep(r1, r)); }
float4 over(float4 dst, float3 col, float a) { return float4(col * a, a) + dst * (1. - a); }

// four cel bands deep → main → hot → light, halftone dots through the main band, a thin highlight, ink rim
float3 celBands(float v, float b1, float2 frag)
{
    float b2 = aastep(.42, v), b3 = aastep(.66, v), b4 = aastep(.9, v);
    float3 col = lerp(_Deep.rgb, _Main.rgb, b2); col = lerp(col, _Hot.rgb, b3); col = lerp(col, _Light.rgb, b4);
    float dots = length(frac(frag / 7.) - .5);
    col = lerp(col, _Hot.rgb, (1. - aastep(smoothstep(.42, .66, v) * .55, dots)) * (b2 - b3));
    col = lerp(col, lerp(_Hot.rgb, _Light.rgb, .5), (b3 - aastep(.71, v)) * .6);
    return lerp(col, _Ink.rgb, b1 - aastep(.235, v));
}

// race symbol for the sigil centre, < 0 inside: 0 heart, 1 crescent, 2 water drop, 3 claw marks
float symbol(float2 c, float motif)
{
    float2 p = float2(c.x, -c.y);
    if (motif < .5) return sdHeart(p / .4 + float2(0., .52)) * .4;
    if (motif < 1.5) return max(length(p) - .36, -(length(p - float2(.14, .1)) - .3));
    if (motif < 2.5)
    {
        float2 q = p + float2(0., .06);
        return min(length(q) - .22, max(abs(q.x) - (.22 - q.y * .5), q.y < 0. ? 1. : max(-q.y, q.y - .42)));
    }
    float d = 1.;
    for (int k = -1; k <= 1; k++)
    {
        float2 q = rot2(p - float2(k * .14, 0.), .35);
        d = min(d, max(abs(q.x) - .035 * (1. - abs(q.y) / .38), abs(q.y) - .38));
    }
    return d;
}

// The effects are authored in gamma space like the web preview (palette values are sRGB numbers); the canvas
// blends in linear space, so the colour is converted here. Without this, faint glow tails read several times
// brighter than intended and the edges of their quads show.
float4 toLinear(float4 c)
{
    #ifndef UNITY_COLORSPACE_GAMMA
    bool premul = c.a > .004;
    float3 g = premul ? min(c.rgb / c.a, 4.) : c.rgb;
    g = GammaToLinearSpace(max(g, 0.));
    c.rgb = premul ? g * c.a : g;
    #endif
    return c;
}

fixed4 finish(float4 c, v2f i)
{
    c = toLinear(c) * i.color.a;
    #ifdef UNITY_UI_CLIP_RECT
    c *= UnityGet2DClipping(i.world.xy, _ClipRect);
    #endif
    return c;
}
#endif
