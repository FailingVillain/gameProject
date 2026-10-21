# game-tools/image — 유닛 스프라이트 파이프라인 (v0.5)

## 규격
- 프레임 128×160 px, 발 기준점 (64,144). 행 = 방향 s/se/e/ne/n (sw/w/nw 는 게임이 반전). 열 = 포즈 프레임(아래 anims 순서, 보병 기본 16열 → 시트 2048×800).
- LOD: 같은 모델을 64×80(기준점 32,72, `<name>@64`)과 32×40(16,36, `<name>@32`)으로도 **각각 네이티브 렌더**(다운스케일 아님). 테두리는 128 에서 2px, 64/32 에서 1px (`voxel.Res.outline_px`).
- 세력 색: `<name>_team.png` 회색조 레이어(철모 띠 + 상완 패치). 음영: 재질별 8단계 셀 셰이딩(`voxel.NS`), 그라데이션 없음.
- 모델 좌표계는 64×80 기준(`common.BASE_FW`). 픽셀 치수는 전부 `voxel.Res(fw, fh)` 에서 파생된다 — 숫자 하드코딩 금지.
- 시트 1장은 4096px 를 넘지 않는다. 열이 넘치면 `<name>_1.png, _2.png …` 로 나뉘고 `<name>_layout.json` 이 `cells{frame: {sheet, col}}` 로 위치를 가리킨다(행 = 방향 인덱스).
- 게임(korean-division)은 아직 64×80 로더 — 게임 쪽 갱신은 별도 작업. `export.py` 는 게임에 실행하지 말 것.

## 명령
- `python make_unit.py <name> [--preview] [--out DIR]` (= `make_unit.bat`) → `<game-tools>/korean-division-image-out/<faction>/<name>/`
  - 진영 폴더 자동 분기(`tools/outroute.py` `faction_of`, make_unit·make_vehicle 공용): `-japanese`→`japanese/`, `-fengtian`→`fengtian/`, `-russian-white`→`russian-white/`, `-russian-red`→`russian-red/`, `tank-*`·`supply-wagon`→`vehicles/`, `-old`·`infantry-ww2`→`_legacy/`, 나머지(접미사 없음)→`korean/`. `--out` 을 주면 그 루트 아래에 같은 규칙으로 들어간다. 건물은 `buildings/` 고정(make_building.py).
  - 출력: 시트/팀/anchors/layout ×3 LOD, `contact_<name>.png`(라벨 붙은 검수용, 팀색 빨강, 2배), `contact_<name>_lod.png`, `gif/<name>_<anim>_<dir>.gif`
  - `--preview`: s·e × idle, walk_1, fire_a 만 128×160 (빠른 반복용). 전체 보병 1유닛 ≈ 30초.
- 구 생성기 `tools/vox_*.py`(scout/worker/at_infantry/tank/artillery/towed/fx)는 레거시: 일부는 돌아가지만 `out/` 에 쓰고 재작업 예정.

## 구조
- `tools/voxel.py` 엔진(프리미티브, 레이캐스트, 셀 셰이딩, 리그 `build_figure`). 모든 Prim 에 `.src ∈ {body, headgear, gear, weapon}` 태그 — 나중에 장비를 별도 레이어로 렌더하기 위한 분리점(지금은 한 장으로 합침).
- `tools/parts/` 재사용 파츠(함수가 프리미티브 리스트 반환): `headgear.py`(m1_helmet, m1_helmet_net, side_cap, officer_cap), `rifles.py`(garand+bayonet, carbine), `sidearms.py`(pistol, holster), `blades.py`(sabre, scabbard, map_case), `packs.py`(field_pack, canteen, ammo_pouches, binocular_case), `uniforms.py`(재질 세트 us_od/us_hbt/us_scout/us_officer).
- 무기 여러 개(장교): `units/*.json` 에 `"weapons": {"pistol": "pistol", "sabre": "sabre"}`, 포즈의 `use` 키가 손에 든 것을 고름(나머지는 gear 로 holster/scabbard 에). 장교 anims(26열, `bases/officer.py`): idle(칼자루에 손), walk×4, fire_a/fire_c(권총 서서/무릎), idle_alt(검으로 지시), death, charge×4(검 치켜든 돌격), melee×3(베기), death_b, death_c.
- `tools/bases/<base>.py` 포즈 세트(`pose_for(frame, d)`) — 몸은 공유, 변형은 장비만 다르게. `anims/<base>.json` 애니메이션 → 프레임 목록(+GIF ms; 멈춤 구간은 프레임 복제 대신 긴 ms). 포즈 키 `attach=["bayonet"]`(무기 extras 부착), `e_pull`(E방향에서 무기를 몸 쪽으로 당기는 모델단위), `tilt`(눕기/쓰러짐).
- 보병 anims(35열; `walk_fire` = walk_fire_1~4, 걷기 다리 위상 1~4 + 조준 상체, 2·4번 프레임에 총구 섬광): idle, walk×4, fire_a/b/c(aim+fire), idle_alt, death(hit/fall/dead), spare, charge×4, melee×3, melee_b×2, idle_lean, go_prone, fire_prone(aim_prone/fire_prone), death_b(hit/stagger_b/kneel_b/dead_b), death_c(hit/stagger_c/slump_c/dead_c).
- `units/<name>.json` 유닛 정의(~12줄): base, anims, headgear, uniform, weapon, gear[], team[], dirs, frames.
- `tools/unit_loader.py` JSON → Kit/Weapon/poses, `tools/contact.py` 검수 시트.

## 변형 추가 방법
1. `units/infantry-default.json` 복사 → 이름 바꾸고 headgear/weapon/gear/uniform 만 수정. 새 파츠가 필요하면 `tools/parts/*.py` 에 함수 1개 + 레지스트리 dict 등록.
2. 새 모션: `anims/<base>.json` 에 anim 추가 + `bases/<base>.py pose_for` 에 포즈 분기 추가 (열은 자동으로 늘고 시트는 자동 분할).
3. `python make_unit.py <name> --preview` 로 확인 후 전체 렌더.

## 관통 검사 (collide.py, 보정 완료) — 규칙: 완성 유닛은 0 hit
- `python tools/collide.py <unit> [frame...] [--hist]` / `make_unit.py` 자동 실행. 무기(src=weapon) 표면 샘플 → 몸통·머리(+모자)·다리·팔·장비, 장비(gear) → 다리·팔·머리. 출력: `frame/dirs: weapon>torso 2.3, gear>leg 1.8` (최대 깊이, 모델단위).
- 보정: TOL 1.5(히스토그램: 정상 접촉 ≤1.5, 실제 관통 2~5), 잡은 손(`grip`/`("t",..)`)의 손·전완·팔꿈치는 손 중심 GRIP_R 6 이내 무시, 몸통/골반 안에 숨은 허벅지 뿌리 무시, 장비↔몸통(장착면)은 검사 안 함. 부위 태그는 `voxel.build_figure`(leg_L/R, upper_/elbow_/forearm_/hand_L/R, head).
- 다인원 장면(crew·포병·기병): `Prim.fig`(= `build_figure` 호출 단위)로 인물을 나누고, 인물 안의 weapon/gear 검사 + 인물이 아닌 프리미티브(총·포·바퀴·말·밧줄 = mount)가 각 인물 몸(몸통/머리/다리/팔)을 뚫는지 검사한다(`collide.contacts_multi`, 키 `mount>leg[2]` = 2번째 인물). 의도된 접촉 제외: 손 GRIP_R 6 이내(밧줄·손잡이·고삐), 팔은 REACH_R 11 이내(바퀴·트레일에 손 뻗음), 기병의 안장·등자(leather/brass/steel)↔다리, 허벅지↔말통(horse) 2.5 까지. 유닛은 `collide_prims(frame, dir)` 를 제공(crew/포병은 방향 무관 → s 만). `python tools/collide.py heavy-maxim|heavy-howitzer|heavy-infantry-gun|heavy-field-gun|cavalry-default`. 승무원 위치는 `tune/poses.json artillery.<종류>` stations/travel(push_x, shield_xk, unpack_t, unpack_out) 로 조정.
- 포즈·장비 위치를 바꾸면 5유닛(infantry-default/regular/japanese-test/ww2, officer) 모두 0 hit 인지 확인. E방향 pull 은 관통이 생기지 않는 만큼만(사격·melee_2 는 0).

## 규칙
- Claude 는 렌더 결과를 직접 보지 않는다. 수치 검사(기준점 편차, 테두리 접촉, 색 수, 시트 크기)만 하고 사용자에게 `contact_<name>.png` 전체 경로를 준다.
- 모델 치수·포즈는 모델 단위(64×80 기준)로 쓰고, 픽셀 단위 값은 `Res` 를 통해서만.

## 미세조정 값 = JSON (코드 수정 불필요, 숫자만)
- `tune/materials.json` `ramps.<재질>` = 앵커색 4개[r,g,b] 어두움→밝음 (8단계는 자동). "바지 더 어둡게" → `lkhaki`/`khaki` 앵커 낮추기. `order` 는 건드리지 말 것(렌더 동점 처리 순서).
- `tune/parts.json` 모자(`headgear.feldkappe|officer_cap|japanese_cap|stahlhelm|m1_helmet`: `pos`[x,y,z]·`size`/`r`·`h`·`tilt`), 장비(`gear.field_pack|scabbard|holster…`), 무기(`weapons.bolt_rifle|garand` barrel.z1 = 총 길이, `weapons.sabre.blade` = [길이, 피치°] 폴리라인, `pistol`). "모자 조금 크게" → 해당 `size`/`r` ×1.05. 칼 평평하게 → `sabre.blade` 피치 값 줄이기.
- `tune/poses.json` `render`(BASE_ZOOM 전체 크기, STRIPE_K 바지줄 폭, RBOX_ROUND 둥글기, 전역 전용), `infantry`(port_arms/shoot/charge 무기 위치, `e_pull`·`e_yaw` E방향 당김), `officer`(`wave`/`wave_side` 칼 휘두르기 팔·각, `idle_alt_sword`, `melee_arms`, `charge_arm`, `pistol`).
- 한 유닛만 바꾸려면 `units/<name>.json` 에 `"tune": {"parts": {...}, "poses": {...}, "materials": {"ramps": {...}}}` (바꿀 키만 적으면 deep-merge). `_doc` 키는 설명용. 코드: `tools/tune.py`, `tools/parts/_t.py`.
- 값 바꾼 뒤 `python make_unit.py <name> --preview`. 동일성 확인은 `--out` 으로 전/후 PNG 비교.

## 차량 (make_vehicle.py, tools/vehicles/)
- `python make_vehicle.py <name> [--preview] [--out DIR]` — `units/<name>.json` 에 `"kind": "vehicle"`, `"model"`(renault_ft / armored_car), `"turret"` 종류. 유닛: tank-ft-female(MG), tank-ft-male(37mm), tank-armored_car(MG, model armored_car).
- 차체 시트 `<name>_hull[@144|@72].png`(288×288, 접지 기준점 (144,192)) + 포탑 시트 `<name>_turret[@120|@60].png`(240×240, 피벗 = 포탑링 중심 (120,132)) 를 따로 낸다. 9행 = s/sse/se/ese/e/ene/ne/nne/n (22.5°), 서쪽 7방향은 게임이 반전(`mirror_map`). 줌은 보병 모델 줌 ×1.8(`vehcommon.VEH_ZOOM`, 이전 ×1.2 의 1.5배), 테두리·AO 는 같은 `Res` 규칙.
- `_hull_layout.json` 의 `turret.mount{dir: [dx,dy]}` = 접지점 기준 포탑링 화면 오프셋(px). 게임은 포탑 셀의 피벗(`anchor`)을 `차체 기준점 + mount[차체 방향]` 에 맞춰 어떤 포탑 방향이든 덮어 그린다. 포탑 프레임은 자동 이동 없음(피벗 고정).
- 열: 차체 idle, move_1~4, dead / 포탑 idle, fire, fire_2, dead (`anims/vehicle.json`). 검수: `contact_<name>_hull.png`, `contact_<name>_turret.png`, 합성 `contact_<name>.png`(포탑 +90°/+225°, 반전 열 포함), `contact_<name>_lod.png`, gif/(rotate 16방향, turret_spin).
- 재질은 `vehcommon.register_material` 로 voxel 테이블에 추가(veh_od/veh_khaki/veh_track/veh_steel/veh_rubber/veh_burn…). voxel.py 는 손대지 않음.

## 중화기 유닛 (heavy-*)
- 이름 규칙: 카테고리 접두사 `heavy-` — `heavy-machinegunner`(LMG 보병, **앉아쏴**: 한쪽 무릎 + 양각대를 편 총을 낮은 모래주머니 흉벽(`parts/heavy_weapons.py` `_sandbag_wall`, extra `bipod_sandbag`) 위에 올리고 개머리판 어깨 견착, 오른손 권총손잡이·왼손 개머리판 밑받침. 총 위치는 `bases/infantry_heavy.py` `_mg_fire` 가 월드 좌표로 풀고 반동 적용 — 프레임 이름 mg_aim/mg_fire_a/mg_fire_b 는 그대로. **조준 방향 (N2, 2026-10-19)**: 자세는 모든 방향에서 같고 (관통 0), 포즈 키 `view_yaw` 로 그림 전체를 돌려 투영된 총열이 행 방향과 정확히 일치 (mg_kneel · mg_down · mg_aim; 검사 `python tools/mg_axis.py <unit>` 최대 오차 0°, 사격 칸 ≤ 1.3°). 경기관총 5종은 `"dirs": "16"` (9행 22.5°, 서쪽 반전 = 16방향) + `"frame_wide": 1.5` (칸 192×160 / 96×80 / 48×40, 줌 동일, `unit_loader.WideRes`) — 게임은 레이아웃의 dirs · frame 을 그대로 읽는다), `heavy-at-rifleman`(대전차소총, **무릎쏴**: 한쪽 무릎 + 어깨 견착, 총 길이 51.2 = 개머리판 -19.2 ~ 총구 32), `heavy-maxim`(막심 중기관총 2인 조). 포즈 파일 `tools/bases/infantry_heavy.py`, 무기 `tools/parts/heavy_weapons.py`.
- at 애니: idle, walk×4, deploy(at_kneel, at_set, at_aim), fire_kneel(aim, fire_a/b 반복), pack(역순), death/death_b/death_c.
- 2인 조 유닛(`"kind": "crew"`, `tools/crew.py`): 한 스프라이트에 병사 2명 + 총. 프레임 192×192, 접지 기준점 (96,128) (가로 중앙, 아래서 2/3), LOD 96×96 (48,64) / 48×48 (24,32). 줌은 보병과 동일(`CrewRes`, pxs 2/1/0.5). 손 IK 는 월드 좌표 목표를 `voxel.LAST['T']` 로 몸통 좌표로 변환. `make_unit.py` 가 그대로 렌더(`--preview` 는 crew 미지원). 열: heavy-maxim = idle, move×4(사수가 밧줄로 견인, 탄약수가 탄약상자 운반), deploy(set_1/set_2/aim), fire(fire_a/b), pack(역순), death(hit/fall/dead). 프레임마다 모델을 이동해 발자국 중심을 기준점에 맞춤(이동/대기 vs 사격 자세 간 총 위치가 달라짐).
## 기병 (cavalry, make_unit.py 공용)
- `units/cavalry-default.json`(base `cavalry`): 보병 공용 몸(야전회색 상의·카키 승마바지·승마장화·Feldkappe) + 말(`tools/parts/horse.py`, 갈색 bay 8단계 + 검은 갈기·꼬리·다리). 무기 `{"carbine": "cav_carbine", "sabre": "sabre"}` — 손에 없을 때 카빈은 등에 멤(`parts.json horse.carbine_carry` = slung | boot), 군도는 안장 왼쪽 칼집.
- 프레임 192×224, 기준점 (96,190) = 말 몸통 중심 아래 지면. LOD 96×112 (48,95), 48×56 (24,48). 줌은 보병과 동일(기수 = 보병 크기). `bases/cavalry.py` 가 `LODS`·`render_unit_frame` 을 제공 → `unit_loader`/`make_unit` 가 그걸 쓴다(관통 검사는 건너뜀).
- 26열(`anims/cavalry.json`, 모든 다프레임 anim shared_fit): idle/idle_2, walk×4, gallop×4, aim/fire, charge×4, melee×3, hit/fall/dead(말과 함께 쓰러짐), hit_b/slip_b/fall_b/dead_b(기수 낙마).
- **기병 장교** `units/cavalry-officer.json` (+ `-fengtian`): 기병 모델 복사 + 장교 장비 (officer_cap · 놋쇠 테두리 · 권총집 · 지도 가방), 무기 `{"pistol", "sabre"}` (카빈 없음) → aim/fire 가 한 손 권총 자세 (`bases/cavalry.py _pistol`, `tune/poses.json cavalry.pistol`, 왼손 고삐).
- 조정값: `tune/parts.json horse`(scale·몸통/다리/목/머리 치수·안장·굴레), `tune/poses.json cavalry`(걸음·습보 키 포즈, 안장 자세, 고삐 손, 사격 위치, charge_arm/melee_arms). 렌더 ≈ 6분.
## 포병 (heavy-howitzer / heavy-infantry-gun / heavy-field-gun, `"kind": "artillery"`)
- `python make_unit.py heavy-howitzer [--preview] [--out DIR]` (같은 make_unit.py). 유닛 json: `"gun"`(howitzer|infantry_gun|field_gun), `"frame"`[W,H], `"ay_frac"`, `"zoom_mul"`(1.0 = 보병 줌), `"center"`[x,z]. 코드: `tools/artillery_unit.py`(ArtRes = VehRes 기반 큰 프레임, 줌은 보병 기준), `tools/parts/guns.py`(포·바퀴·포가·방패·포탄·섬광), `tools/bases/artillery.py`(포수 배치·포즈·장면), `anims/artillery.json`.
- 프레임: 곡사포 624×456 (기준점 (312,286), 말 견인 move 용으로 확대; LOD 312×228 / 156×114), 야전포 384×312 (192,200), 보병포 336×272 (168,190); LOD 는 1/2·1/4. 기준점 = 포 발판 중심(`center`)의 지면, 모든 셀 동일(자동 이동 없음). 5방향 + 반전(승무원 비대칭도 같이 뒤집힘).
- 열 14: idle, fire_0/1/2(섬광·후좌), load_1/2, unpack_1/2(= pack 은 역순 재생), move_1~4, hit, dead. move = 포신 뒤로 돌려 사람이 포미(트레일)를 밧줄로 끌고/바퀴·방패를 밂. **곡사포는 말 견인 구현됨**(`"tow": true`, `bases/towing.py`: 포 트레일을 림버에 걸고 말 2필, 림버 위 2명 착석 + 2명 보행, 말 걸음·바퀴 동기, `tune/poses.json artillery.howitzer.tow`, `tow_center` = 팀 중심 z; 야전포·보병포는 아직 인력 견인) / 훅 설명: 이동 프레임의 트레일 아이 `pts["eye"]` 가 layout json `hitch_px{move_n:{dir:[x,y]}}` 로 기록됨, `bases/artillery.py` 의 `MOVE_PULLERS`/`pull_crew` 를 limber+말 팀으로 교체.
- 수치 튜닝: `tune/parts.json guns.<종류>`(전 길이 ×scale), `tune/poses.json artillery.<종류>`(고각·후좌·승무원 위치 stations·travel). 재질 gun_* 7종은 materials.json 끝에 추가.
- 섬광은 overlay 프리미티브 → voxel.make_frame 이 몸체 z 버퍼로 깊이 검사(연기 없음).

## 건물 (make_building.py, tools/buildings/)
- `python make_building.py <house-thatch|house-tile|house-brick|house-barn|village-capture|houses|all>` → `<out>/buildings/<name>/` (시트 `<name>[@64|@32].png` + `_team`, `_layout.json`, `contact_<name>.png`) + `buildings_overview.png`.
- 1타일 = 12 모델단위, 128/64/32 px per tile (LOD), 고정 방향(yaw 0), 기준점 = 발자국 중앙 지면. 테두리 (8,8,8) 3/2/1px. 상태 = 시트 칸(`layout.cells`), 시트 4096px 초과 시 격자.
