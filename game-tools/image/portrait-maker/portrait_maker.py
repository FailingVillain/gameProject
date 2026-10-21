"""
유닛 초상화 생성기 — SD.Next(Stable Diffusion) API 로 후보를 만들고, 고른 것을 게임 초상화(320×320 픽셀풍)로 내보낸다.

  python portrait_maker.py                 대화형 메뉴 (초상화만들기.bat)
  python portrait_maker.py gen <유닛|all> [장수] [세력]   후보 생성
  python portrait_maker.py all [장수=16] [세력]          모든 병과마다 후보 생성 (기본 16장씩)
  python portrait_maker.py pick <유닛> <후보파일>          후보를 게임 초상화로 적용 (+ export.py 실행)
  python portrait_maker.py list            유닛 · 세력 목록

SD.Next: C:\\works\\StableDiffusion\\sdnext (API http://127.0.0.1:7860). 꺼져 있으면 켜고 준비될 때까지 기다린다.
프롬프트는 prompts.json 에서 고친다 (유닛별 설명 + 세력별 군복 · 공통 화풍).
"""
import base64
import io
import json
import os
import random
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.request

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
IMAGE_ROOT = os.path.dirname(HERE)                       # coding/game-tools/image
OUT = os.path.join(IMAGE_ROOT, "out")
CANDIDATES = os.path.join(HERE, "candidates")
API = "http://127.0.0.1:7860"
SDNEXT_BAT = r"C:\works\StableDiffusion\sdnext\run_sdnext.bat"
PORTRAIT = 320


def load_prompts():
    with open(os.path.join(HERE, "prompts.json"), encoding="utf-8") as f:
        return json.load(f)


# ---------------------------------------------------------------- SD.Next

def api(path, payload=None, timeout=600):
    data = json.dumps(payload).encode() if payload is not None else None
    req = urllib.request.Request(API + path, data=data, headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return json.loads(r.read().decode("utf-8"))


def api_ready():
    try:
        api("/sdapi/v1/sd-models", timeout=5)
        return True
    except (urllib.error.URLError, OSError, ValueError):
        return False


def ensure_sdnext(wait_seconds=600):
    if api_ready():
        return True
    if not os.path.isfile(SDNEXT_BAT):
        print(f"[오류] SD.Next 실행 파일이 없습니다: {SDNEXT_BAT}")
        return False
    print("SD.Next 를 켭니다 (새 창). 모델을 불러오는 데 1~3분 걸립니다...")
    subprocess.Popen(["cmd", "/c", SDNEXT_BAT], cwd=os.path.dirname(SDNEXT_BAT),
                     creationflags=getattr(subprocess, "CREATE_NEW_CONSOLE", 0))   # 새 콘솔 창 (닫으면 SD.Next 종료)
    t0 = time.time()
    while time.time() - t0 < wait_seconds:
        time.sleep(5)
        if api_ready():
            print("SD.Next 준비 완료.")
            return True
        print(f"  기다리는 중... {int(time.time() - t0)}초")
    print("[오류] SD.Next 가 응답하지 않습니다. SD.Next 창의 오류를 확인하세요.")
    return False


# ---------------------------------------------------------------- 생성 · 후처리

def pixelize(img: Image.Image, colors: int, grid: int) -> Image.Image:
    """정사각 자르기 → grid×grid 로 줄이고 색 수 제한 → 320×320 로 nearest 확대 (게임 픽셀풍)."""
    w, h = img.size
    s = min(w, h)
    img = img.crop(((w - s) // 2, (h - s) // 2, (w - s) // 2 + s, (h - s) // 2 + s)).convert("RGB")
    small = img.resize((grid, grid), Image.LANCZOS)
    small = small.quantize(colors=colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGBA")
    return small.resize((PORTRAIT, PORTRAIT), Image.NEAREST)


def generate(unit: str, count: int, faction: str):
    cfg = load_prompts()
    units = cfg["units"]
    if unit not in units:
        print(f"[오류] 알 수 없는 유닛 '{unit}'. 가능: {', '.join(units)}")
        return
    fac = cfg["factions"].get(faction, cfg["factions"][cfg["defaultFaction"]])
    style = cfg["style"]
    prompt = ", ".join(p for p in [units[unit]["prompt"], fac["uniform"], style["prompt"]] if p)
    negative = ", ".join(n for n in [style["negative"], units[unit].get("negative", "")] if n)   # 병과별 제외 프롬프트 추가 가능
    gen = style["generation"]
    d = os.path.join(CANDIDATES, unit)
    os.makedirs(d, exist_ok=True)
    print(f"\n[{unit} · {faction}] {count}장 생성\n  프롬프트: {prompt}")
    for i in range(count):
        seed = random.randint(1, 2**31 - 1)
        payload = {
            "prompt": prompt, "negative_prompt": negative, "seed": seed,
            "width": gen["width"], "height": gen["height"], "steps": gen["steps"],
            "cfg_scale": gen["cfg"], "batch_size": 1, "n_iter": 1,
        }
        if gen.get("sampler"):
            payload["sampler_name"] = gen["sampler"]
        t0 = time.time()
        res = None
        for attempt in range(3):   # 켜진 직후에는 모델을 아직 불러오는 중일 수 있다 · 샘플러 이름이 틀리면 기본값으로
            try:
                res = api("/sdapi/v1/txt2img", payload)
            except (urllib.error.URLError, OSError) as e:
                print(f"  [오류] 생성 실패: {e}")
                return
            if res.get("images"):
                break
            print(f"  이미지가 비어 있음 — 다시 시도 ({attempt + 1}/3)")
            payload.pop("sampler_name", None)
            time.sleep(10)
        if not res or not res.get("images"):
            print(f"  [오류] SD.Next 가 이미지를 돌려주지 않습니다: {str(res)[:300]}")
            return
        raw = Image.open(io.BytesIO(base64.b64decode(res["images"][0].split(",", 1)[-1])))
        name = f"{unit}_{faction}_{seed}"
        raw.save(os.path.join(d, name + "_원본.png"))
        pixelize(raw, style["pixel"]["colors"], style["pixel"]["grid"]).save(os.path.join(d, name + ".png"))
        print(f"  {i + 1}/{count}  {name}.png  ({time.time() - t0:.1f}초)")
    print(f"후보 폴더: {d}")


def generate_all(count: int = 16, faction: str | None = None):
    """모든 병과(prompts.json 의 units)마다 count 장씩 후보 생성. 9병과 × 16장 ≈ 26분."""
    cfg = load_prompts()
    faction = faction or cfg["defaultFaction"]
    units = list(cfg["units"])
    t0 = time.time()
    print()
    print(f"병과 {len(units)}개 × {count}장 = {len(units) * count}장 생성 시작 (세력 {faction})")
    for u in units:
        generate(u, count, faction)
    print()
    print(f"전체 완료: {(time.time() - t0) / 60:.1f}분, 후보 폴더 {CANDIDATES}")


def pick(unit: str, candidate: str):
    d = os.path.join(CANDIDATES, unit)
    path = candidate if os.path.isabs(candidate) else os.path.join(d, candidate)
    if not path.lower().endswith(".png"):
        path += ".png"
    if not os.path.isfile(path):
        print(f"[오류] 후보 파일 없음: {path}")
        return
    img = Image.open(path).convert("RGBA")
    if img.size != (PORTRAIT, PORTRAIT):
        img = pixelize(img, load_prompts()["style"]["pixel"]["colors"], load_prompts()["style"]["pixel"]["grid"])
    dst = os.path.join(OUT, unit)
    os.makedirs(dst, exist_ok=True)
    img.save(os.path.join(dst, f"portrait_{unit}.png"))
    team = os.path.join(dst, f"portrait_{unit}_team.png")
    if os.path.isfile(team):
        os.remove(team)   # 손그림 초상화의 세력 색 레이어는 새 그림과 맞지 않는다
    print(f"적용: {dst}\\portrait_{unit}.png")
    export = os.path.join(IMAGE_ROOT, "export.py")
    if os.path.isfile(export):
        subprocess.run([sys.executable, export], cwd=IMAGE_ROOT)
        print("게임 테마로 내보냈습니다 (export.py).")


# ---------------------------------------------------------------- 메뉴

def menu():
    cfg = load_prompts()
    units = list(cfg["units"])
    factions = list(cfg["factions"])
    while True:
        print("\n===== 유닛 초상화 생성기 =====")
        print("1) 후보 생성   2) 후보 적용(게임 초상화로)   3) 후보 폴더 열기   4) 병과별 16장씩 전체 생성   0) 끝")
        c = input("선택: ").strip()
        if c == "0" or c == "":
            return
        if c == "4":
            fi = input(f"세력 (Enter = {cfg['defaultFaction']}): ").strip() or cfg["defaultFaction"]
            if fi not in factions:
                print("잘못된 세력"); continue
            if ensure_sdnext():
                generate_all(16, fi)
            continue
        if c == "3":
            os.makedirs(CANDIDATES, exist_ok=True)
            os.startfile(CANDIDATES)
            continue
        print("유닛: " + "  ".join(f"{i + 1}){u}" for i, u in enumerate(units)) + ("  a)전체" if c == "1" else ""))
        u = input("유닛 번호: ").strip().lower()
        chosen = units if (u == "a" and c == "1") else [units[int(u) - 1]] if u.isdigit() and 0 < int(u) <= len(units) else []
        if not chosen:
            print("잘못된 선택")
            continue
        if c == "1":
            print("세력: " + "  ".join(f"{i + 1}){f}" for i, f in enumerate(factions)))
            fi = input(f"세력 번호 (Enter = {cfg['defaultFaction']}): ").strip()
            fac = factions[int(fi) - 1] if fi.isdigit() and 0 < int(fi) <= len(factions) else cfg["defaultFaction"]
            n = input("유닛당 장수 (Enter = 4): ").strip()
            n = int(n) if n.isdigit() and int(n) > 0 else 4
            if not ensure_sdnext():
                continue
            for unit in chosen:
                generate(unit, n, fac)
        else:
            unit = chosen[0]
            d = os.path.join(CANDIDATES, unit)
            files = sorted(f for f in os.listdir(d) if f.endswith(".png") and not f.endswith("_원본.png")) if os.path.isdir(d) else []
            if not files:
                print("후보가 없습니다. 먼저 생성하세요.")
                continue
            for i, f in enumerate(files):
                print(f"  {i + 1}) {f}")
            k = input("적용할 번호: ").strip()
            if k.isdigit() and 0 < int(k) <= len(files):
                pick(unit, files[int(k) - 1])


def main():
    a = sys.argv[1:]
    if not a:
        menu()
    elif a[0] == "gen" and len(a) >= 2:
        if not ensure_sdnext():
            sys.exit(1)
        cfg = load_prompts()
        units = list(cfg["units"]) if a[1] == "all" else [a[1]]
        for u in units:
            generate(u, int(a[2]) if len(a) > 2 else 4, a[3] if len(a) > 3 else cfg["defaultFaction"])
    elif a[0] == "all":
        if not ensure_sdnext():
            sys.exit(1)
        cfg = load_prompts()
        generate_all(int(a[1]) if len(a) > 1 else 16, a[2] if len(a) > 2 else cfg["defaultFaction"])
    elif a[0] == "pick" and len(a) == 3:
        pick(a[1], a[2])
    elif a[0] == "list":
        cfg = load_prompts()
        print("유닛:", ", ".join(cfg["units"]))
        print("세력:", ", ".join(cfg["factions"]))
    else:
        print(__doc__)


if __name__ == "__main__":
    main()
