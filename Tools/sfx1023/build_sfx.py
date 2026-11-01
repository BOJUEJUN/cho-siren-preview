"""CHO-SIREN battle SFX v1 (2026-10-23): layered from Sonniss GDC 2026 part 1 (royalty-free) +
lentikula Basic Spell Impacts (CC0) + small synth layers. Three variants per event.
    venv/bin/python build_sfx.py  ->  ../out/<event>-<n>.wav"""
import sys, os, glob
import numpy as np
sys.path.insert(0, os.path.dirname(__file__))
from sfxlib import *

OUT = '/Users/bojuejun/cho-siren-audio-src/out/'
os.makedirs(OUT, exist_ok=True)
G = lambda pat: glob.glob(SON + '*/' + pat + '*')[0]

punch_src = load(G('FGHTImpt_4 x Punch'))
punches = [fade(seg(punch_src, t, .24), .001, .1) for t in (0.43, 1.44, 2.43, 3.45)]   # the hit of each pair
sword_src = load(G('WEAPSwrd_Sword Slide Cuts'))
swords = [seg(sword_src, t, .7) for t in (0.15, 2.26, 4.34, 6.34, 8.48, 10.82)]
wind = load(G('WINDDsgn_Wind, Rush, Whoosh'))
metal_whoosh = load(G('METLMisc_Metal, Slow Whoosh'))
water = load(G('WATRMisc_Water, Liquid Impact'))
bells_src = load(G('MAGMisc_Magic Christmas Bells'), 0, 6)
bells = [seg(bells_src, t, .9) for t in (0.34, 1.71, 3.59, 4.72)]
bass16 = load(G('DSGNBass_Bass Drop & Downer Fast 16'), 0, 1.4)
bass12 = load(G('DSGNBass_Bass Drop & Downer Fast 12'), 0, 1.5)
bass04 = load(G('DSGNBass_Bass Drop & Downer Medium 04'))
wood_src = load(G('WOODImpt_Wooden Hit'), 0, 6)
woods = [seg(wood_src, t, .5) for t in (2.27, 3.93, 4.62)]
bang_src = load(G('METLImpt_Metal Bangs'), 0, 3)
bangs = [seg(bang_src, t, .45) for t in (0.26, 0.76, 1.27, 1.68)]
evil = load(G('AMBDsgn_Evil Spell'), 0.25, 2.0)
shield_src = load(G('WEAPArmr_Metal Shield Spin'), 0, 6)
L = lambda kind, i: load(LEN + f'{kind} Spell Impacts/{kind} Spell Impact {i}.wav')

def pick(lst, v): return lst[v % len(lst)]

def hit(v):
    p = pick(punches, v); p = speed(hp(p, 90), (1.18, 1.3, 1.1)[v])
    w = pick(woods, v); w = speed(hp(w, 200), 1.4)
    swish = hp(pick(swords, v + 3), 2500)[:int(.12 * SR)]
    x = mix(.45, (p, 0, 0), (w, .0, -9), (crack(.025, 4500, v), 0, -5), (fade(swish, .001, .08), 0, -9),
            (sub_drop(130, 48, .28, .12), 0, -5))
    return bus(x, tail=.15)

def crit(v):
    p = speed(hp(pick(punches, v + 1), 90), 1.05)
    s = hp(pick(swords, v * 2), 900)
    b = pick(bells, v)
    x = mix(.95, (s, 0, -3), (p, .01, 0), (crack(.03, 4200, v + 7), .01, -8), (sub_drop(150, 40, .45, .2), .01, -3),
            (lp(bass16, 400), .0, -8), (hp(b, 2500), .03, -13))
    return bus(x, verb=.08, tail=.3)

def strike_charm(v):
    f = fade(L('Fire', (1, 3, 5)[v])[:int(.7 * SR)], .001, .3)
    b = hp(pick(bells, v + 1), 3000)
    x = mix(.8, (hp(f, 250), 0, 0), (b, .02, -6), (sparkle((1760, 2637, 3520), .5, .03, .1, v), .04, -14))
    return bus(x, phone_bass=False, tail=.3)

def strike_demon(v):
    e = hp(evil[int(v * .4 * SR):][:int(.9 * SR)], 120)
    m = speed(pick(bangs, v), .7)
    x = mix(.9, (fade(e, .002, .4), 0, -2), (lp(m, 2500), 0, -4), (sub_drop(90, 35, .5, .25), 0, -6))
    return bus(x, tail=.35)

def strike_mermaid(v):
    wv = speed(water, (1.0, 1.12, .9)[v])
    lw = fade(L('Water', (2, 4, 5)[v])[:int(.8 * SR)], .001, .35)
    x = mix(.9, (hp(wv, 150), 0, 0), (hp(lw, 200), .02, -4), (sparkle((2093, 3136), .4, .05, .08, v + 3), .08, -16))
    return bus(x, phone_bass=False, tail=.3)

def strike_bloodelf(v):
    s = hp(pick(swords, v * 2 + 1), 700)
    ice = fade(L('Ice', (1, 3, 4)[v])[:int(.6 * SR)], .001, .25)
    x = mix(.85, (s, 0, -1), (hp(ice, 1500), .03, -5), (sub_drop(110, 45, .3, .12), .02, -9))
    return bus(x, tail=.3)

def ultimate(v):
    rise = noise_swell(.55, 1200, 5000, v) * 0.5
    tt = np.arange(int(.55 * SR)) / SR
    rise = rise + (np.sin(2 * np.pi * np.cumsum(220 + 900 * (tt / .55) ** 2) / SR) * (tt / .55) ** 3 * .35).astype(np.float32)
    p1 = speed(hp(pick(punches, v), 80), .85); p2 = speed(hp(pick(punches, v + 2), 80), 1.0)
    light = fade(L('Lightning', (1, 3, 5)[v])[:int(1.4 * SR)], .001, .6)
    b = hp(pick(bells, v + 2), 2500)
    x = mix(2.2, (rise, 0, -10), (hp(metal_whoosh, 300), .1, -10),
            (p1, .55, 0), (p2, .62, -2), (bass04[int(3.8 * SR):][:int(1.6 * SR)], .5, -2),
            (sub_drop(160, 32, .9, .45), .55, -2), (crack(.04, 3500, v), .55, -6),
            (hp(light, 300), .56, -6), (b, .7, -10))
    return bus(x, verb=.14, tail=.6)

def ko(v):
    ice = fade(L('Ice', (2, 5, 1)[v])[:int(1.0 * SR)], .001, .4)
    m = speed(pick(bangs, v + 1), 1.2)
    x = mix(1.4, (hp(ice, 400), 0, -1), (lp(m, 4000), 0, -6), (lp(bass12, 300), 0, -5),
            (sub_drop(140, 34, .7, .3), 0, -3), (sparkle((3136, 3951, 4699, 5274), .9, .045, .18, v), .12, -12))
    return bus(x, verb=.12, tail=.45)

def hurt(v):
    p = speed(lp(pick(punches, v + 3), 3500), (.82, .9, .78)[v])
    m = lp(speed(pick(bangs, v + 2), .8), 3000)[:int(.3 * SR)]
    x = mix(.6, (p, 0, 0), (fade(m, .001, .15), 0, -8), (sub_drop(100, 40, .3, .12), 0, -6), (crack(.02, 2200, v + 11), 0, -8))
    return bus(x, tail=.2)

def block(v):
    s = shield_src[int((.44, 1.33, 3.73)[v] * SR):][:int(.7 * SR)]
    x = mix(.8, (hp(s, 300), 0, 0), (sparkle((2349, 3520), .5, .02, .09, v + 5), 0, -10), (sub_drop(120, 60, .15, .06), 0, -12))
    return bus(x, phone_bass=False, tail=.3)

TARGET = {'hit': -16, 'crit': -14, 'strike-charm': -16, 'strike-demon': -16, 'strike-mermaid': -16, 'strike-bloodelf': -16,
          'ultimate': -13.5, 'ko': -14, 'hurt': -16, 'block': -17}

def level(y, target):
    """Event loudness (RMS) to the target, then a final limiter keeps the peak under -1 dBFS."""
    import numpy as np
    rms = 20 * np.log10(np.sqrt((y ** 2).mean()) + 1e-9)
    y = y * db(target - rms)
    c = db(-1)                       # soft ceiling (JUCE's Limiter adds make-up gain, so not that)
    return (np.tanh(y / c) * c).astype(np.float32)

EVENTS = {'hit': hit, 'crit': crit, 'strike-charm': strike_charm, 'strike-demon': strike_demon,
          'strike-mermaid': strike_mermaid, 'strike-bloodelf': strike_bloodelf, 'ultimate': ultimate,
          'ko': ko, 'hurt': hurt, 'block': block}

if __name__ == '__main__':
    import numpy as np
    for name, fn in EVENTS.items():
        for v in range(3):
            y = level(fn(v), TARGET[name]); save(OUT + f'{name}-{v + 1}.wav', y)
            rms = 20 * np.log10(np.sqrt((y ** 2).mean()) + 1e-9)
            print(f'{name}-{v + 1}: {len(y) / SR:.2f}s rms={rms:.1f}dB')
