"""Looks through the game checkout for what is still missing and proposes jobs (少爷: "不要只用我来说，你自己想").
Each rule is cheap and data-driven; add a rule when the game gains a new kind of asset. The page lists the suggestions;
「加入」 appends the target to briefs.auto.yaml, which the engine merges into the briefs."""
import json
import re
from pathlib import Path

# The four original lobby idols use their art names, not member ids.
LOBBY_CLIP_FOR = {"xingli": "catalena", "feiyin": "lilith", "yeying": "elf", "wubai": "mermaid"}


def _members(game: Path):
    try:
        data = json.loads((game / "Assets/Resources/Data/member-catalog.json").read_text(encoding="utf-8"))
        rows = data if isinstance(data, list) else (data.get("Members") or data.get("members") or [])
    except Exception:
        return []
    out = []
    for m in rows:
        mid = m.get("Id") or m.get("id")
        if mid and (game / f"Assets/Resources/Art/Members/member-{mid}.png").exists():
            out.append({"id": mid, "name": m.get("Name") or m.get("name") or mid, "rarity": m.get("Rarity") or m.get("rarity") or ""})
    return out


def suggestions(game: Path, briefs: dict):
    out = []
    members = _members(game)
    # 1. Lobby idle clips: members with art but no idle loop.
    idles = game / "Assets/StreamingAssets/LobbyIdols"
    for m in members:
        clip = LOBBY_CLIP_FOR.get(m["id"], m["id"])
        if not (idles / f"{clip}-a.mp4").exists():
            out.append({"brief": "lobby-idle", "target": {"id": m["id"], "look": ""}, "why": f"{m['name']}还没有大厅待机动画"})
    # 2. Ultimate clips: members not in the manifest.
    try:
        manifest = json.loads((game / "Assets/Resources/UltimateVideos/manifest.json").read_text(encoding="utf-8"))
        have = {e.get("characterId") for e in manifest.get("entries", [])}
    except Exception:
        have = set()
    for m in members:
        if m["id"] not in have:
            out.append({"brief": "ult-cutin", "target": {"id": m["id"], "look": "", "color": "neon"}, "why": f"{m['name']}放大招时只有立绘，没有动画"})
    # 4. Bosses whose entrance video is off.
    boss_src = game / "Assets/Scripts/Systems/Tactics/StageBoss1103.cs"
    if boss_src.exists():
        for name, art, video in re.findall(r'Name = "([^"]+)", Art = "([^"]+)", EntranceVideo = (true|false)', boss_src.read_text(encoding="utf-8")):
            if video == "false":
                out.append({"brief": "boss-entrance", "target": {"id": Path(art).name, "name": name, "art": art + ".png", "element": "dark energy"},
                            "why": f"首领「{name}」出场没有入场动画"})
    # 5. Stage backgrounds: stages with no scene art.
    scenes = game / "Assets/StreamingAssets/LevelScenes1102"
    try:
        tactics = json.loads((game / "Assets/Resources/Data/tactics.json").read_text(encoding="utf-8"))
        stage_ids = [s["Id"] for s in tactics.get("Stages", [])]
    except Exception:
        stage_ids = []
    for sid in stage_ids:
        short = sid.replace("stage-", "")
        if not (scenes / f"{sid}.jpg").exists():
            out.append({"brief": "stage-bg", "target": {"id": sid, "stage": short}, "why": f"{short} 没有专属关卡背景，用的是通用图"})
    # 6. Enemy entrances: every enemy figure without an approved entrance clip yet (the factory's own export).
    for art in sorted((game / "Assets/Resources/Art/Enemies").glob("*-ai-v1.png")):
        eid = art.name.replace("-ai-v1.png", "")
        out.append({"brief": "minion-enter", "target": {"id": eid}, "why": f"小怪 {eid} 登场只有淡入，没有动作"})
    # Keep only suggestions for briefs that exist and targets not already listed there.
    result = []
    for s in out:
        b = briefs.get(s["brief"])
        if not b:
            continue
        key = s["target"].get("id")
        if any((t.get("id") or t.get("race")) == key for t in b.get("targets", [])):
            continue
        s["title"] = b.get("title", s["brief"])
        s["key"] = f"{s['brief']}:{key}"
        result.append(s)
    return result
