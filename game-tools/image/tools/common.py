"""
game-tools/image 공용 도구 — 모든 유닛 생성기가 이 규격을 따른다 (게임 쪽 로더가 이 규격을 읽는다).

[유닛 시트 규격 v0.5 — 128×160, 16열]
  프레임 FW×FH = 128×160 px (RGBA). 시트 = 5행(방향) × 16열 = 2048×800 px. 모든 픽셀 치수는 FW/FH 에서 파생된다
  (모델 좌표계는 64×80 시절 그대로, voxel.PXS = FW/64 로 확대 → 2배 줌, 테두리 2px).
  열 배치:
      0 idle · 1~4 walk (접지, 통과, 접지(반대 다리), 통과) · 5/6 aim/fire A (선 자세) · 7/8 aim/fire B (낮춘 자세)
      · 9/10 aim/fire C (무릎 / 기댐) · 11 idle_alt · 12 hit · 13 fall · 14 dead · 15 spare(= idle 복사)
      worker: 5~10 = 공구 휘두르기 3종(내려치기 / 옆으로 쓸기 / 찍기), at_infantry: 바주카 자세 3종.
  행(방향, 화면 기준 · 탑뷰 + 약간 쿼터뷰, 고도각 36°):
      0 = S  (화면 아래 = 카메라 쪽, 얼굴 · 가슴이 보임)
      1 = SE
      2 = E  (오른쪽, 옆모습)
      3 = NE
      4 = N  (화면 위, 등 · 배낭이 보임)
    SW · W · NW 는 게임이 SE · E · NE 를 좌우 반전해서 쓴다 → 반전해도 자연스럽게 (글자 · 비대칭 표식 금지).
  사망 프레임(13·14)의 기준점은 골반 바로 아래 지면이며 그림이 프레임 안에 들도록 자동 이동한다 (anchors json 에 기록).
  발 위치(기준점) ANCHOR = (FW/2, FH*0.9) = (64, 144). 게임은 유닛 위치에 이 점을 맞춘다. 발밑 그림자는 그리지 않는다(게임이 그림).
  음영: 재질마다 8단계 셀 셰이딩(voxel.NS = 8, 그라데이션 없음). 테두리 1겹(voxel.OUTLINE_PX).
  (게임 korean-division 은 아직 64×80 을 읽는다 — 게임 쪽 로더 갱신은 별도 작업.)

[세력 색]
  같은 크기의 team 시트(<name>_team.png): 세력 색이 들어갈 픽셀만 회색조(밝기 = 음영)로, 나머지는 투명.
  게임이 회색조 × 세력 색으로 칠해 본 시트 위에 덮는다. 위치: 철모 띠 + 어깨 휘장 (발밑 표시는 게임이 그림).
  본 시트에서 그 자리는 비워 두거나(투명) 테두리만 그린다.

[초상화] 320×320 RGBA, 상반신(가슴 위), 정면에서 약간 왼쪽을 향함, 배경은 어두운 단색 또는 투명. team 레이어(같은 크기) 선택.

[출력] 새 파이프라인: python make_unit.py <name> → <game-tools>/korean-division-image-out/<name>/
       (<name>.png, <name>_team.png, <name>_anchors.json, contact_<name>.png, gif/). 구 vox_*.py 는 out/<unit>/ 에 쓴다.
       export.py 가 게임 테마(coding/<game>/assets/themes/ww2/, 기본 korean-division)로 복사한다.
"""
from __future__ import annotations

import os
from typing import Callable, Iterable

import numpy as np
from PIL import Image

BASE_FW, BASE_FH = 64, 80    # 모델 좌표계가 설계된 기준 해상도 (voxel 모델 치수는 이 해상도 기준) — 바꾸지 말 것
FW, FH = 128, 160            # 프레임 크기
PXS = FW / BASE_FW           # 픽셀 배율 (기준 해상도 대비) — 선 두께 · 줌 · 미리보기 배율이 여기서 파생
DIRS = ["s", "se", "e", "ne", "n"]
DIRS16 = ["s", "sse", "se", "ese", "e", "ene", "ne", "nne", "n"]     # 9 rows + mirror = 16 directions (units/<name>.json "dirs": "16")
FRAMES = ["idle", "walk_1", "walk_2", "walk_3", "walk_4", "aim_a", "fire_a", "aim_b", "fire_b", "aim_c", "fire_c",
          "idle_alt", "hit", "fall", "dead", "spare"]
ANCHOR = (FW // 2, FH * 9 // 10)     # 발 위치 (64, 144)
PORTRAIT = 320
PREVIEW_SCALE = max(1, int(round(4 / PXS)))   # 미리보기 · GIF 확대 배율 (기준 해상도에서 4배였다)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "out")

# 2차대전 미군 팔레트 (RGBA). 생성기는 가능한 이 색들을 쓴다 (전체 통일감).
P = {
    "outline":   (24, 22, 18, 255),
    "od_dark":   (58, 62, 38, 255),    # 올리브드랩 (어두움)
    "od":        (86, 90, 54, 255),
    "od_light":  (114, 118, 74, 255),
    "khaki_dark": (110, 96, 66, 255),  # 카키 바지
    "khaki":     (146, 128, 90, 255),
    "khaki_light": (176, 158, 116, 255),
    "helmet_dark": (62, 66, 44, 255),  # M1 철모
    "helmet":    (92, 98, 64, 255),
    "helmet_light": (124, 130, 88, 255),
    "net":       (70, 60, 40, 255),    # 철모 위장망
    "skin_dark": (150, 104, 76, 255),
    "skin":      (196, 146, 110, 255),
    "skin_light": (222, 178, 140, 255),
    "leather_dark": (70, 46, 28, 255), # 각반 · 탄띠 · 신발
    "leather":   (104, 72, 44, 255),
    "canvas":    (150, 140, 104, 255), # 각반 · 배낭 캔버스
    "canvas_light": (180, 170, 130, 255),
    "wood_dark": (84, 54, 30, 255),    # 개런드 개머리판
    "wood":      (124, 82, 46, 255),
    "steel_dark": (40, 42, 46, 255),   # 총열
    "steel":     (70, 74, 80, 255),
    "steel_light": (118, 124, 132, 255),
    "flash_core": (255, 250, 210, 255), # 총구 섬광
    "flash":     (255, 210, 90, 255),
    "flash_edge": (230, 120, 40, 220),
    "eye":       (30, 26, 22, 255),
}


class Frame:
    """프레임 하나 = 본 이미지 + 세력 색 레이어."""

    def __init__(self, w: int = FW, h: int = FH):
        self.base = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        self.team = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        self.w, self.h = w, h

    def px(self, x: int, y: int, color, team: bool = False):
        if 0 <= x < self.w and 0 <= y < self.h:
            if team:
                g = color if isinstance(color, int) else int(sum(color[:3]) / 3)
                self.team.putpixel((x, y), (g, g, g, 255))
                self.base.putpixel((x, y), (0, 0, 0, 0))
            else:
                self.base.putpixel((x, y), color)

    def rect(self, x0, y0, x1, y1, color, team=False):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.px(x, y, color, team)

    def ellipse(self, cx, cy, rx, ry, color, team=False):
        for y in range(int(cy - ry), int(cy + ry) + 1):
            for x in range(int(cx - rx), int(cx + rx) + 1):
                if ((x - cx) / max(rx, 0.5)) ** 2 + ((y - cy) / max(ry, 0.5)) ** 2 <= 1.0:
                    self.px(x, y, color, team)

    def line(self, x0, y0, x1, y1, color, width: int = 1, team=False):
        n = max(abs(x1 - x0), abs(y1 - y0), 1)
        for i in range(int(n) + 1):
            x = round(x0 + (x1 - x0) * i / n)
            y = round(y0 + (y1 - y0) * i / n)
            for dy in range(width):
                for dx in range(width):
                    self.px(x + dx - width // 2, y + dy - width // 2, color, team)

    def blit_map(self, ox: int, oy: int, rows: list[str], legend: dict, team_keys: Iterable[str] = ()):
        """문자 픽셀 맵 그리기. legend: 문자 → 팔레트 키(또는 RGBA). '.' / ' ' = 투명. team_keys 문자는 세력 색 레이어로."""
        team_keys = set(team_keys)
        for y, row in enumerate(rows):
            for x, ch in enumerate(row):
                if ch in ". ":
                    continue
                c = legend[ch]
                c = P[c] if isinstance(c, str) else c
                self.px(ox + x, oy + y, c, team=ch in team_keys)

    def outline(self, color=P["outline"], width: int = 1):
        """불투명 영역(본 + 세력 색) 바깥에 width px 테두리 (4방향 팽창을 width 번)."""
        base = np.array(self.base)
        op = (base[..., 3] > 0) | (np.array(self.team)[..., 3] > 0)
        grown = op.copy()
        for _ in range(max(1, int(width))):
            pad = np.pad(grown, 1)
            grown = grown | pad[1:-1, 2:] | pad[1:-1, :-2] | pad[2:, 1:-1] | pad[:-2, 1:-1]
        ring = grown & ~op
        base[ring] = color
        self.base = Image.fromarray(base, "RGBA")


def save_sheet(name: str, frames: Callable[[str, str], Frame], out_dir: str | None = None):
    """frames(dir, frame) → Frame 를 받아 시트 · 세력 시트 · 미리보기 · GIF 저장. out_dir 기본 = OUT/<name>."""
    d = out_dir or os.path.join(OUT, name)
    os.makedirs(d, exist_ok=True)
    sheet = Image.new("RGBA", (FW * len(FRAMES), FH * len(DIRS)), (0, 0, 0, 0))
    team = Image.new("RGBA", sheet.size, (0, 0, 0, 0))
    grid: dict[tuple[str, str], Frame] = {}
    for r, dname in enumerate(DIRS):
        for c, fname in enumerate(FRAMES):
            f = frames(dname, fname)
            grid[(dname, fname)] = f
            sheet.paste(f.base, (c * FW, r * FH))
            team.paste(f.team, (c * FW, r * FH))
    sheet.save(os.path.join(d, f"{name}.png"))
    team.save(os.path.join(d, f"{name}_team.png"))
    _preview(name, d, sheet, team)
    for dname in DIRS:
        _gif(os.path.join(d, f"{name}_{dname}_walk.gif"), [grid[(dname, f"walk_{i}")] for i in (1, 2, 3, 4)], 130)
        for v in "abc":
            _gif(os.path.join(d, f"{name}_{dname}_fire_{v}.gif"), [grid[(dname, f"aim_{v}")], grid[(dname, f"fire_{v}")], grid[(dname, f"aim_{v}")]], 120)
        if "dead" in FRAMES:
            _gif(os.path.join(d, f"{name}_{dname}_death.gif"), [grid[(dname, "hit")], grid[(dname, "fall")], grid[(dname, "dead")]], [250, 250, 1500])
    return sheet, team


def composite(base: Image.Image, team: Image.Image, color=(70, 140, 235)) -> Image.Image:
    """미리보기용: 세력 색을 입힌 합성 이미지 (team 회색조 × color)."""
    t = np.array(team.convert("RGBA")).astype(np.int32)
    g = t[..., 0:1]
    tinted = np.concatenate([g * color[0] // 255, g * color[1] // 255, g * color[2] // 255, t[..., 3:4]], axis=-1)
    tinted = np.where(t[..., 3:4] > 0, tinted, 0).astype(np.uint8)
    out = Image.new("RGBA", base.size, (0, 0, 0, 0))
    out.alpha_composite(base.convert("RGBA"))
    out.alpha_composite(Image.fromarray(tinted, "RGBA"))
    return out


def _preview(name, d, sheet, team, scale=PREVIEW_SCALE):
    bg = Image.new("RGBA", sheet.size, (78, 110, 64, 255))   # 풀밭 색 배경
    bg.alpha_composite(composite(sheet, team))
    bg = bg.resize((sheet.size[0] * scale, sheet.size[1] * scale), Image.NEAREST)
    bg.save(os.path.join(d, f"preview_{name}.png"))


def _gif(path, frames: list[Frame], ms, scale=PREVIEW_SCALE):
    imgs = []
    for f in frames:
        bg = Image.new("RGBA", (FW, FH), (78, 110, 64, 255))
        bg.alpha_composite(composite(f.base, f.team))
        imgs.append(bg.resize((FW * scale, FH * scale), Image.NEAREST).convert("P"))
    imgs[0].save(path, save_all=True, append_images=imgs[1:], duration=ms, loop=0)


def save_portrait(name: str, base: Image.Image, team: Image.Image | None = None):
    d = os.path.join(OUT, name)
    os.makedirs(d, exist_ok=True)
    assert base.size == (PORTRAIT, PORTRAIT), f"초상화는 {PORTRAIT}×{PORTRAIT}"
    base.save(os.path.join(d, f"portrait_{name}.png"))
    if team is not None:
        team.save(os.path.join(d, f"portrait_{name}_team.png"))
    prev = composite(base, team) if team is not None else base
    prev.save(os.path.join(d, f"preview_portrait_{name}.png"))
