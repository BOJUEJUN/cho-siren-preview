#!/usr/bin/env python3
"""Writes Builds/qa-logs/zj-1031/audio/audition.html — every SFX candidate per event (chosen ones marked,
with metrics and the previous shipped take for A/B), plus all voice takes (defeat / enemy barks / announcer).
Relative links only, so the folder can be zipped and opened anywhere. No network, no keys."""
import json
import html
import pathlib

REPO = pathlib.Path(__file__).resolve().parents[2]
LOG = REPO / 'Builds/qa-logs/zj-1031/audio'
OUT = LOG / 'audition.html'

LABEL = {'hit': '普攻命中（通用）', 'crit': '暴击', 'hurt': '我方受击', 'block': '格挡', 'shield': '护盾',
         'heal': '治疗', 'ko': '敌人击破', 'strike-charm': '魅族·普攻材质层', 'strike-demon': '恶魔·普攻材质层',
         'strike-mermaid': '人鱼·普攻材质层', 'strike-bloodelf': '血精灵·普攻材质层', 'ultimate': '大招切入/大招命中',
         'boss-hit': 'Boss 受击（加重层，新）', 'down': '我方倒地（新）', 'grade-perfect': '大招判定 PERFECT（新）',
         'grade-great': '大招判定 GREAT（新）', 'grade-good': '大招判定 GOOD（新）', 'grade-miss': '大招判定 MISS（新）'}


def rel(p):
    return pathlib.Path(p).resolve().relative_to(LOG.resolve()).as_posix() if str(p).startswith(str(LOG)) else \
        pathlib.Path('../../../..', p).as_posix()


def audio(src):
    return '<audio controls preload="none" src="%s"></audio>' % html.escape(src)


def main():
    sfx = json.loads((LOG / 'sfx-candidates.json').read_text())
    rows = []
    for ev, data in sfx.items():
        prev = sorted((LOG / 'previous-sfx').glob(ev + '*.wav'))
        rows.append('<section><h2>%s <code>%s</code> <small>目标短时响度 %s LUFS-M</small></h2>' %
                    (LABEL.get(ev, ev), ev, data['target_lufs_m']))
        rows.append('<table><tr><th>候选</th><th>试听</th><th>入选</th><th>时长</th><th>起音</th><th>短时响度</th>'
                    '<th>真峰值</th><th>峰均比</th><th>频谱质心</th><th>削波</th><th>Suno 主体</th></tr>')
        for c in data['candidates']:
            chosen = c.get('chosen_as')
            rows.append('<tr class="%s"><td>%s</td><td>%s</td><td>%s</td><td>%.2fs</td><td>%.0f ms</td><td>%.1f</td>'
                        '<td>%.1f dBTP</td><td>%.1f dB</td><td>%d Hz</td><td>%d</td><td>%s @%.2fs</td></tr>' % (
                            'chosen' if chosen else '', pathlib.Path(c['path']).stem,
                            audio(pathlib.Path(c['path']).relative_to('Builds/qa-logs/zj-1031/audio').as_posix()),
                            ('★ ' + chosen) if chosen else '', c['dur'], c['attack_ms'], c['momentary_max'],
                            c['tp_db'], c['crest_db'], c['centroid'], c['clip_count'],
                            html.escape(c['body_source']), c['body_start']))
        for p in prev:
            rows.append('<tr class="prev"><td>旧版 %s</td><td>%s</td><td>(10-23 版)</td><td colspan="8"></td></tr>' %
                        (p.name, audio('previous-sfx/' + p.name)))
        rows.append('</table></section>')

    vm = json.loads((REPO / 'Docs/AUDIO-1031-voice-manifest.json').read_text())
    vc = json.loads((LOG / 'voice-candidates.json').read_text())
    rows.append('<section><h2>我方阵亡台词（日语，中文字幕）</h2><table><tr><th>资源</th><th>日语</th><th>字幕</th>'
                '<th>声线</th><th>入选 take</th><th>所有 take</th></tr>')
    for d in vm['defeat']:
        takes = ' '.join('<span class="%s">%s %s</span>' % ('chosen' if t == d['chosen_take'] else '', t[-1],
                         audio('candidates/voice/defeat/%s.wav' % t)) for t in d['takes'] if t in vc)
        rows.append('<tr><td>%s</td><td>%s</td><td>%s</td><td>%s / %s</td><td>★ %s</td><td>%s</td></tr>' % (
            d['resource'], html.escape(d['text']), html.escape(d['zh']), d['voice_id'], d['emotion'],
            d['chosen_take'], takes))
    rows.append('</table></section>')
    rows.append('<section><h2>大招播报员（英文）</h2><table><tr><th>档位</th><th>入选</th><th>三种声线对比</th></tr>')
    for d in vm['announcer']:
        takes = ' '.join('<span class="%s">%s %s</span>' % ('chosen' if t == d['chosen_take'] else '',
                         t.split('-', 2)[2], audio('candidates/voice/announcer/%s.wav' % t)) for t in d['takes'])
        rows.append('<tr><td>%s</td><td>★ %s</td><td>%s</td></tr>' % (d['text'], d['voice_id'], takes))
    rows.append('</table></section>')
    rows.append('<section><h2>敌人台词（barks.json）</h2><table><tr><th>敌人</th><th>类别</th><th>日语</th><th>字幕</th>'
                '<th>声线</th><th>试听</th></tr>')
    for d in vm['enemy']:
        rows.append('<tr><td>%s</td><td>%s</td><td>%s</td><td>%s</td><td>%s%s</td><td>%s</td></tr>' % (
            d['enemy'], d['resource'].split('-')[-2], html.escape(d['text']), html.escape(d['zh']), d['voice_id'],
            (' + ' + d['fx']) if d.get('fx') else '',
            audio('candidates/voice/enemy/%s.wav' % d['resource'].split('/')[-1])))
    rows.append('</table></section>')

    OUT.write_text('''<!doctype html><html lang="zh"><meta charset="utf-8"><title>CHO-SIREN 音频试听 2026-10-31</title>
<style>body{font:14px/1.5 -apple-system,"PingFang SC",sans-serif;background:#120a22;color:#eee;margin:24px}
h1{color:#1ee8e8}h2{color:#ff6ad5;margin-top:32px}small{color:#aaa;font-weight:normal}code{color:#1ee8e8}
table{border-collapse:collapse;width:100%%}td,th{border-bottom:1px solid #2c2245;padding:4px 6px;text-align:left;vertical-align:middle}
tr.chosen{background:#0f3a3a}tr.prev{opacity:.7}span.chosen{outline:2px solid #1ee8e8;border-radius:4px;padding:2px}
audio{height:28px;width:220px;vertical-align:middle}</style>
<h1>CHO-SIREN 音频试听 · 2026-10-31</h1>
<p>★ = 已放进游戏的文件（Assets/Resources/Audio/...）。音效每个事件 5–6 个候选：主体为 Suno v6 Sounds 生成，叠加 Sonniss GDC 2026 /
lentikula 真实录音层（低频、瞬态、点缀、尾音），统一短时响度、真峰值 ≤ -1 dBTP、零延迟起音。想换某个候选：
把该 cand 文件复制到 <code>Assets/Resources/Audio/Sfx/&lt;事件名&gt;.wav</code>（或 -2 / -3）即可，代码不用改。详见 Docs/AUDIO-1031.md。</p>
%s</html>''' % '\n'.join(rows))
    print(OUT)


if __name__ == '__main__':
    main()
