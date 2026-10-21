# HANDOFF 아카이브 (2026-10)

> `docs/HANDOFF.md` 에서 옮긴 완료 기록 (원문 그대로; N-번호 중복은 HANDOFF 와 같이 뒤쪽에 접미사). 옛 상태 · 완료 작업 확인용 — 새 세션은 여기를 읽지 않는다 (필요할 때만 Grep).

## A. 최근 완료 (2026-10-19)

## 최근 완료 (2026-10-19)
- ✅ **전술 AI 3단계 3A (s3a 병합)** — S1 기동 계획 (정면 · 측면 · 협공 · 중점, 고정 → 기동 도착 → 돌격 동기, 우회 ≤ 정면 ×2 · 120초) + S14 미션 목표 → 과제 + 미션별 철수 문턱 (`aiWithdrawAt` · `aiCommitment`). 모두 프로필 키 뒤, 겹침 `korean+stage2` 로 2단계 비교. FormatVersion 31, `KD_SELFTEST_ONLY=s3a`. 결과 = STAGE3_REVIEW '3A 결과' (T_r1.5 측면 8/8 선택 · 승 6/8 ✅, 순서 62~75% ❌ (목표 80%), 협공 손실 45% ❌ (목표 27%, engage 병합 뒤 기준선 변화)). **다음**: 3B (방어 · 예비 · 역습 · 교착) — 3A 남은 일 4개 포함.
- ✅ **ai412 병합** — 결정 4 돌격 허가 (신호 · 엄호 + 병력비, 예비 150초) · 결정 12 대대 역할 (40명 미만 대대 제외, 단일 전투 대대 분할) · 부사관 기본 편성 (미션 보병 분대장 하사) · CA2014 수정. 상태 추가 없음 (FormatVersion 26), 골든 `7d54df98fd24ec0a b11ef8aa06add8ff`, selftest 1141 OK (`KD_SELFTEST_ONLY=ai412`). 측정 TACTICS_OBS '9차'. 사용자 결정 대기: 부사관 배수 재조정, B 공격 기동 중대 접근 (B 승 4/5 → 2/5).


## B. 일시 정지 상태 (2026-10-18 저녁, 정지 해제됨)

## ⏸ 현재 상태 — 일시 정지 (2026-10-18 저녁, 사용자 지시 "다음 세션에 진행")
**운영 규칙 (필수)**: 무거운 작업 (dotnet 빌드 · 전체 `--selftest` · `--stress` · `tools/detcheck.ps1` · 대량 렌더) 은 **동시 2개까지**. 2026-10-18 17:26 PC 강제 종료 (빌드 · 시험 · 성능 측정 · 대량 렌더 4개 겹침). 성능 측정은 상한 명시 (≤4000기, ≤600틱, 시드 ≤3). 여러 커밋 측정은 빌드 → 측정 → 정리 하나씩. 모델: 下 haiku / 中 sonnet / 上 opus, Fable 금지. push 는 사용자 명령 시만.

### 저장소 상태
| 저장소 | 위치 · 브랜치 | 마지막 커밋 | 상태 |
|---|---|---|---|
| 게임 main | `coding/korean-division` | `dcde7ca` (정리 묶음 2 병합) + 문서 커밋 | 깨끗. FormatVersion 18, replay 8. 940 OK selftest, detcheck 골든 갱신 (8변형 동일) |
| 전술 AI 2단계 | (main 병합 `54e9e28`) | — | ✅ 완료 · 워크트리 삭제 |
| 그림 도구 main | `coding/game-tools` | `db09f06` | 깨끗 (출력 폴더 · 오디오 폴더는 미추적). 일괄 그림 0~4단계 커밋 |
| 남은 워크트리 | `coding/kd-terrain-tiles` | — | 캐시만, 권한 문제로 미삭제 (사용자: `git worktree remove --force ../kd-terrain-tiles`) |


## C. 밤샘 실행 계획 (2026-10-19) · 점검 기록 · 진행 중 메모

### 🌙 밤샘 실행 계획 (사용자 2026-10-19) — 무거운 작업 동시 최대 3~4, 끝나는 대로 다음을 띄움
#### 밤샘 점검 기록
- 00:37 check: OK. running formations, ground-target (merging into main), lmg-aim, artillery renders (field gun 11 min each, 10 units ~2h). RAM 16/31 GB free. No stops.
- 00:5x N1 ground-target merged (5dd8c4d, FormatVersion 21). Launched N7 auto-company.
- 01:xx formations merged (7bc137a, FV22; AI templates default OFF until stage 2). Launched N3. TODO for N4: role-group overlap push (-19 tiles), add formations.json/tactics.json to Defs.Hash.
- 01:3x N7 done on branch auto-company (42173e9), NOT merged (lmg-aim merge in progress on main). Merge auto-company after lmg-aim lands. Field/inf gun renders: 3/10 done (~11-12 min each), script continues alone; howitzer step needs a new agent after ALLDONE.
- 02:xx N2 merged (both repos), N7 merged 33b6ef1, N3 merged 5941aee (FV23, 1057 OK). Launched N4. Pending user checks: officers balance values (handover 5/8 s, XP thresholds), major/colonel only via missions, temp promotions in missions.
- 01:37 check: OK. running N4 templates2 + gun renders (7/10 done, ~6-7 min each now). RAM 17 GB free. No stops, no waste found.

진행 중: 中 정리 묶음 (bundle3) · 上 전투 대형 + 전술 대형 1단계 (formations) · 中 포 렌더 (곡사포 5 + 보병포/야전포 10).
- ✅ **전투 대형 + 전술 대형 1단계 (formations) 병합** (Snapshot v22): 분대 대형 5종 · 기병 사선 · 포병 대형 (접촉 시 횡대 전개) · 대대 파상, 역할 5종 자동 배정 · 카드 재배정, 틀 5종 정지 배치. 자료 `assets/data/formations.json` · `tactics.json`, 테스트 `KD_SELFTEST_ONLY=formations`, 상세 ENGINE_NOTES 끝 절. 전술 AI 틀 사용 = 프로필 `tactics.templates` 기본 **꺼짐** (평지 맞대결 6판 틀 쪽 1승 — N4 (2단계 행동) 뒤 켜고 재측정). 시연 콘솔 `after N` · `ftac` · `fco` · `fsq` · `fbat` · `fmove`. N4 는 `CompanyLayout` (CombatFormations.cs) · `TacticRules.StanceOf` 위에 만든다.

## D. 밤샘 대기열 완료 항목 (N-번호, 2026-10-19 이전 ✅ · 완료)

N1. ✅ 中 플레이어 지점 포격 명령 — 완료 (브랜치 ground-target, 카드 "지점 포격" 단축키 T, Snapshot v21, `KD_SELFTEST_ONLY=groundfire`).
N2. ✅ (2026-10-19 lmg-aim / lmg-art 병합) 上 경기관총 사격 방향 · 스프라이트 정합 (4o) — 원인 = 그림 (고정 측면 스윙으로 총열 최대 82° 어긋남 + 고각 투영 + 8방향 · 데드밴드 12°). 그림 전체 view_yaw 회전으로 총열 = 행 방향 (오차 0°), 경기관총 16방향 · 넓은 칸 192/96/48, 행 히스테리시스 (그려진 방향 오차 ≤ 15.25°), 거치 조준 회전 traverseRate 90°/s. ENGINE_NOTES 맨 끝 절, `KD_SELFTEST_ONLY=lmgaim`, 확인 장면 `KD_SCENE=mgaim KD_AIMDEBUG=1 --demo`.
N4. ✅ 上 전술 대형 2단계 — 완료 (브랜치 templates2 병합, Snapshot v26, `KD_SELFTEST_ONLY=drill`): 역할 행동 · 지휘관 전환 · 화력 우세 대기 · 연신 · 종심 방어 참호/역습 · 포병 대형 → 화력-기동, 겹침 수정, Defs.Hash, **중화기 보폭 규칙**, 전술 AI 틀 기본 켬 (측정 교환비 0.98, TACTICAL_AI_STAGE3_PLAN 'N4 측정'). 남은 것: ③ AI 틀 선택 (측면 · 돌격) + P1/P2. 검수용 스크린샷 = 세션 scratchpad `n4b_fire_manoeuvre_30.png` · `n4b_flank_left_30.png`.
N5. 中 포 새 그림 들여오기 (곡사포 hitch/unhitch 매핑 포함) — 포 렌더 뒤.
  → 완료 2026-10-19 (브랜치 n5 병합): 보병포·야전포 변형 + 곡사포 변형 4종 + 갱신 시트 가져옴, 로더·매핑 변경 없음 (layout json 으로 충분), selftest PASS · detcheck GOLDEN · uifuzz 30s 예외 0 (LOD 합 651/768MB), 확인 시트 docs/art_review/n5_contact.png. 보병포·야전포 10종 충돌 검사는 렌더 로그에 없어 미검증.
N7. (완료 2026-10-19: Game.AutoFormBattleCompanies, 골든 불변) 中 전투 테스트 (`--quick-battle` · 교전 테스트) 에서 플레이어 보병도 시작 때 중대로 자동 편성 → 축소 화면 중대 휘장이 아군에도 나옴 (지금은 낱 유닛이라 점으로 표시; 미션은 정상). 시뮬 변경 → 골든 갱신.
N8. ✅ (2026-10-19 render-fixes) 최대 확대 LOD0 · 모션 누락 — 조사: 전 유닛 키 × 진영 변형 × LOD 레이아웃의 애니메이션 · 칸 · 시트 크기 · 투명 칸이 LOD 0/1/2 에서 완전히 일치 (불일치 0건, 전 UsedAnimNames 해석됨), `KD_SCENE=allunits` 줌 4 격자에서 24종 + 4개 변형 전부 LOD0 로 그려짐 · 그림 없는 칸 0. 실제로 보이는 증상의 원인 = LOD0 가 만들어지는 동안 (처음 확대 · 30초 내림 후 · 예산 초과 내림) 가운데 LOD1 이 안 만들어져 있어 가장 작은 LOD2 로만 물러서 그려짐 → `LodAtlas.Resolve` 가 가운데 LOD 도 같이 요청 (+ `물러서 그림` 카운터, uifuzz 요약). 확인용 장면: `KD_SCENE=allunits --demo` (+ `KD_SCENE_PAGE=0..3` 6종 3x2 · `KD_SCENE_GAP` · `KD_SCENE_FIGHT=1|2` · `KD_SCENE_FOEDIST` · `KD_SCENE_COMPANY=1`).
N8b. ✅ 장교 권총 넣었다 뺐다 깜빡임 — `UnitArt.LodFrame`: 쏜 뒤 2.5초 (`AimHoldTicks` 150) 는 표적이 없어도 조준 자세 유지 (사격 칸은 사격 순간만), 그 뒤에 집어넣음. 전 병종 공통 (표시 전용).
N9. ✅ 축소 화면: 부대 지정 유닛 (모든 병종) = 휘장, 미지정 = 팀색 점 — 코드 이미 올바름 (`UnitSymbols.Replaces` = Company != null, 점은 `DrawDots` 가 팀색), `KD_SCENE=allunits KD_SCENE_COMPANY=1 KD_CAMZOOM=0.3` 스크린샷으로 확인.
N10. ✅ (2026-10-19 `artillery2` 병합) 中 포병 재조준 — 사계 밖 표적 · 지점은 옮길 각 < 90° (`combat.traverseRedeployDeg`) 면 방열한 채 포가 회전 (turnRate × `traverseShiftMul` 0.5, 사격 멈춤), 90° 이상만 공격 명령 · 지점 포격이 철수 → 회전 → 재방열 (자동 · 사수는 놓음). 자동 표적은 사계 안 우선 + 돌리면 되는 표적까지, 전술 AI `InGunArc` = `CanTraverseInPlace`. 상태 추가 없음 (FormatVersion 26 그대로). ENGINE_NOTES '방열 사계', `KD_SELFTEST_ONLY=shells` · `groundfire`.
N12. ✅ (2026-10-19 브랜치 `n12` 병합 — 수치 balance.json `combat` gapCheckSec 25 · gapCheckMinSec 20 · gapMoveMin 2.75 · gapCombatSec 10 · regroupFireSec 5 · formArriveSlack 0.6 · formMoveTolerance 0.5 · officerBack 3 · officerSpread 3 · officerLeadTol 0, 상태 추가 없음 (FormatVersion 26), `KD_SELFTEST_ONLY=n12`, ENGINE_NOTES 'N12', 측정 TACTICS_OBS 8차) 上 대형 강박 줄이기 + 장교 위치 + 정찰 관측반 (N4 병합 뒤, 사용자 승인 2026-10-19): (1) 빈자리 점검 20~30초 · 전투 중 안 함, 자리 재배정은 2.5~3칸 넘게 벗어날 때만, 사격 · 엄폐 중이면 대형 복귀 안 함, 도착은 여유 반경, 이동 중 작은 오차 무시 (값 balance.json). (2) 장교 자동 돌격은 유지 (독전 지휘 — 돌격 때 앞장서도 됨). 고칠 것 = 장교가 권총 사거리(6칸) 때문에 혼자 앞으로 나서는 문제: 평소 장교는 중대 전선보다 앞서지 않고 (지휘 위치 = 대열 중앙 뒤 2~4칸), 표적을 쫓아 전진하지 않음. (3) 정찰병 묶음에는 장교 없음: 시나리오 포병 관측반을 정찰병만으로 (관측 장교는 포대 쪽), 정찰병 중대 편성 시 장교 배정 안 함.
N13. 中 지도 크기별 과부하 시험 (5단계 200/400/800/1200/1500): 지도 생성 시간 · 길찾기 (구역 그래프 · 먼 거리) · 미니맵/지형 청크 · 안개 메모리 · 전술 AI 위협 지도 · 저장 크기 · 틱 비용 (≤4000기 상한, 단계마다 하나씩) → 표로 보고.
   -> 완료 (2026-10-19, 브랜치 n13): 면적 비용 = `World.RebuildHash` 칸 전체 clear/접두합 ×2/틱 (1500² 에서 4 ms), 5단계 전부 1000/3000기 60Hz 안전 (최악 5.9ms), 스파이크 70ms 미조사 — `docs/legacy/MAPSIZE_LOAD_2026-10-19.md`. 권고 미적용.
N16. ✅ (2026-10-19) 성능: `World.RebuildHash` 면적 비례 → 유닛 비례 (점유 칸만 지우고 기수 정렬, 칸 안 순서 동일, detcheck GOLDEN 그대로) + 스파이크 원인 = 포탄 구덩이마다 지도 전체 지형 구조 · 참호 묶음 재계산 → 숲 · 가옥 · 참호 변경 때만 다시 계산. 1500² 3000기 평균 4.8 → 2.7 ms, 최대 44 → 10.6 ms. 결과 `docs/legacy/MAPSIZE_LOAD_2026-10-19.md` 8절, 시험 `KD_SELFTEST_ONLY=hash`.
N17. ✅ 수치만 적용 (2026-10-19 사용자 결정, N12 와 함께): tactics.json `superioritySupp` 0.55 → **0.20**, `patienceSec` 60 → **90** (`suppressDecay` · 소총 suppress 그대로). 돌격 조건 로직 (18절 제안 1) 은 아직 — 결과 TACTICS_OBS '8차'. (옛 줄) 화력 우세 수치: 관찰 4차 권고 `superioritySupp` 0.55 → 0.15 (5차에서 감쇠 35 조합 확인 중) + 돌격 조건 로직 (18절 제안 1). 근거 `docs/legacy/TACTICS_OBS_2026-10-19.md` 20~23절.
N18. ✅ (2026-10-19 완료, `docs/legacy/EXCHANGE_TABLE_2026-10-19.md` · `docs/n18_raw/`; 제안은 미적용 — 사용자 승인 대기) 中 (로드맵만 — 사용자 2026-10-19) 병종별 1:1 교환비 표: 소총 중대 대 기관총 · 기병 대 보병 · 돌격보병 · 근위대 등 같은 인원/같은 비용 맞대결 (`--balance`, 시드 ≥5, 전차 · 대전차 소총병 제외) → 표로 보고, 관찰만.
N19. ✅ (2026-10-19 n19) 최대 확대 경기관총 사라짐 — **재현 안 됨, 단일 원인 못 찾음**. 조사: 전 유닛 21종 보병 · 승무원 × 5 변형 × 3 LOD 의 모든 도달 가능 칸 · 방향 행이 시트에 있고 비어 있지 않음 (`KD_SELFTEST_ONLY=lodaudit`, 120 스프라이트 문제 0, 모션 누락 0), 월드 32088틱 훑기 (대기 · 이동 · 사격 · 거치 · 철수 · 16방향 · 피격) 에서 그려지지 않은 틱 0. 경기관총 LOD0 (192×160) 시트 · layout · theme · anchors 정상, 아틀라스 171칸 정상 (7.2 MB), mgaim · 교전 · 퀵배틀 최대 확대 스크린샷과 uifuzz 에서 `KD_LODMISS=1` 누락 기록 0건. 가능한 설명은 LOD0 비동기 생성 중 (처음 확대 · 30초 내림 후) 물러서 그린 작은 LOD (poses_contact 의 흐린 경기관총) 뿐. 방어: `LodDraw.Cell` 이 칸이 비면 다른 준비된 LOD 로 (`LodAtlas.FindCell`), 깨진 (NaN) 방향 값이 행 범위 밖으로 유닛을 통째로 지우지 않게 (`RowOf`/`SnapFacing`). 도구: `KD_LODMISS=1` (못 그린 칸 한 번씩 로그), `KD_SCENE_R` (mgaim 반경), `KD_SHOT_FOLLOW=1 [KD_SHOT_FOLLOW_KEY=종류]` (--shot 카메라가 내 유닛 따라감). 사용자가 다시 보면: 어느 상태 (이동 · 거치 · 사격 · 사망) · 어느 진영인지 + 콘솔의 `[lodmiss]` 줄을 알려 주면 바로 좁힐 수 있음. 비교 사진 docs/art_review/n19_contact.png.
N21. ✅ (2026-10-19 브랜치 `carbine` 병합) 돌격보병 = 카빈 + 수류탄 2 · 한국군 전용 · 돌격 사격 기본 · 근접 권총 — `balance.json` `carbine_assault` · `pistol_assault`, 유닛 `koreaOnly` · `marchFireDefault` · `marchAccMul` · `marchIntervalMul` · `sidearm` · `sidearmRange`. 스냅샷 변경 없음 (Unit.Weapon · Company.MarchFireOn 이 이미 저장), 골든 갱신. 테스트 1178 OK (병합 후). 측정 EXCHANGE_TABLE 'N21' (소총 대 돌격 여전히 0/10). **남은 일**: 카빈 복셀 그림 별도 렌더, ai_kr_vs_ft_1000 기반 기준선(대대 C · 포병 효과) 재측정, 사거리 재조정 사용자 결정.
N20. ✅ (2026-10-19 브랜치 `gapfill` 병합, Snapshot v27 · 리플레이 v10) 上 대열 정비 — 교전 중 빈자리 메우기를 명령으로만: 플레이어 "대열 정비" (단축키 L, 카드 (1,2)/(2,1) · 전투 탭 (0,0)), 장교 판단 (교전 중 6초 × DecideInterval, 벗어난 병사 35% · 앞줄 빈 슬롯 3 · 연속 3, 히스테리시스 ×0.6, 재명령 20초), 병사 숙련 (계급 0~1 신병 곧장 · 2~3 정규 50%/6초 · 4+ 노병 15%/12초, 교전 끝나면 바로). 수치 balance.json `combat` close*, `KD_SELFTEST_ONLY=gapfill`, ENGINE_NOTES 'gapfill', 측정 TACTICS_OBS '10차' (승 · K/E 잡음 범위 — 정비 대상이 대기 병사뿐이라 효과 작음, 후보 3개 사용자 결정 대기).
N21b. ✅ (2026-10-19 브랜치 `artyai` 병합 — 지시서 이름 'N20 포병 AI', 번호 겹쳐 N21) 上 포병 AI: 관측반 전진 (AI 직접 지휘 · 접촉 뒤 전선 뒤 · 적 소총 사거리 × 1.5 밖 · 전멸 시 정찰조 재배치), 준비사격 = 실제 사격 뒤 돌격, 곡사포 돌격 · 약진 표적 우선 + 근접 위험 + 강제 표적 유지, 재배치 쿨다운 (포대 수 정규화 · 쏘는 중 · 최소 이동), 직사포 사거리 규율 · 포 종류별 분견대 (봉천 직사포 늦은 전개 원인 = 저격수와 섞인 분견대). 수치 tactics.json `artilleryAi`, `KD_SELFTEST_ONLY=artyai`, ENGINE_NOTES 'N21 포병 AI', 결과 ARTILLERY_EFFECT 'N21' (재배치 −40~72% · B 승 7→10/12 · 봉천 야전포 첫 사격 69→26초; 대가: C 곡사포 살상 149→108, 돌격 표적 조준 개선 안 됨 — 조정 후보 3개 사용자 결정 대기).
N24. ✅ (2026-10-19 브랜치 `bunchfix` 병합) 上 중대 뭉침 · 비비기 수정 (진단 docs/legacy/BUNCHING_DIAG_2026-10-19.md): 종대 자취 부족 줄 = 접선 반대쪽 외삽 (막히면 꼬리 양옆 바깥 열), 좁은 길 진입 = 폭 선 25% 이상 막힘 (지금 · 3 · 8칸 앞) · 해제 120틱 (balance.json `narrowBlockFrac` · `narrowClearTicks`), 막힌 행군 슬롯 = ShiftSlot + Id 해시 흩뿌림, 미션 배치 대형 지도 안 (`FitDeployInMap`). 저장 형식 변경 없음 (작업값 `RowReach` 만), 골든 갱신. 측정 도구 `--bunch`, `KD_SELFTEST_ONLY=bunchfix`, 결과 진단 문서 '수정 결과' (kr1000 종대 nn 0.68→1.02 · 뭉친 중대-초 837→6 · 가장자리 중대 5초 nn 0.46→1.01). **남은 일**: 종대 에피소드가 여전히 짧음 (중앙값 3.5초), ai_kr_vs_ft_1000 대대 배치가 서로 겹침 (미션 데이터 — 사용자 결정).
N24b. ✅ (2026-10-19 브랜치 `gapfill2` 병합, Snapshot v28) 上 대열 정비 2 — 사용자 승인 3건: 대상 = 대기 · 사수 · 공격 이동 병사 (공격 이동은 정비 깃발 `Close` 로 공격 이동 그대로 슬롯까지, 돌격 · 참호 · 중화기 승무원 · 기동조 · 예비 제외), 틀 행동 중 · 도착한 깃발에서도 판단, 판단 = 앞줄 구멍 (구멍 ≥ max(5, 앞줄 × 50%) · 연속 6, 벗어난 비율 보조 0.9), 적 돌격 12칸 안 접근 → 즉시 판단 (재명령 20초 지킴). 측정 TACTICS_OBS '11차' (병합 뒤 재측정 포함): 명령당 병사 5 → 110~118, 앞줄 구멍 거의 절반, 봉천 돌격 우세 3~4 → 0~1, K/E 0.79 → 0.75 (병합 뒤), 승 13 → 11 (병합 뒤, B 5 → 3), **한국 돌격 후퇴 43 → 60% (다음 결정: 화력조 제외 또는 아군 돌격 중 정비 보류 — 사용자 결정 대기)**. `KD_SELFTEST_ONLY=gapfill`, ENGINE_NOTES 'gapfill2'.
N22. ✅ (2026-10-19 브랜치 `pursuit` 병합, Snapshot v29) 上 추격 · 기병 측면: 후퇴 명령 (속보 + 후퇴 표시 `Retreating`, 모든 후퇴 경로) · 보병은 후퇴하는 적에게 착검 · 자동 · 행진 사격 3칸 · 장교 · 틀 돌격 안 함 (사격만) · 전술 AI 사격 추격 (사거리 × 0.75 로 따라가며 쏨, 1 × 소총 사거리 또는 40초 한계 → 20초 재정비) · 기병 측면 · 후방 우선 (접근 각 점수, 측면 경유점, 기관총 정면 사계 회피, 교착 20초 → 물러나 차장) · 소규모 기병 (8기 미만) 정찰 · 차장 · 기병 추격 허용 (`cavalryChargeFleeing` 기본 true, 사용자 결정). 수치 tactics.json `pursuit` · `cavalry`, `KD_SELFTEST_ONLY=pursuit`, ENGINE_NOTES '추격 · 후퇴 속보 · 기병 측면', 측정 TACTICS_OBS '12차'.
N45. ✅ (2026-10-19 브랜치 `pursuit2` 병합, Snapshot v35) 上 후퇴 사격 (후퇴 중 걸으며 쏨, 명중 × 0.3 · 간격 × 2) · 후퇴 뒤 재정비 (곧바로 재전진 대신 20초 사수 → 조건부 재전진, `regroup`) · 포병만 아군 오사 (+ 포수 규율 `crewDangerMargin`) · 기병 측면 조금 엄격 (`flankOddsMul` 0.7 · `flankMinSupp` 0.15 · `formedMinN` 4 — 소총 30 대 기병 30 = 0/10 → 1/1/8) · `ai_kr_vs_ft_1000` (+ tools 복제본) 대대 종대 배치 (겹침 해소, C 기준선 변경). `KD_SELFTEST_ONLY=pursuit2`, ENGINE_NOTES '후퇴 사격 · 후퇴 뒤 재정비 · 포병만 아군 오사', 측정 TACTICS_OBS '13차 (후퇴 사격 · 재전진 · 포병 오사)' (`tools/tobs_agg13p.py`, `docs/tactics_obs_raw13p/`).
N25. ✅ (2026-10-19 브랜치 `engage` 병합, Snapshot v30 · 리플레이 v11) 上 교전 거리 · 엄폐 (사용자 결정 1 · 2 + 방어 지형 활용): 반응 거리 `engageRangeFrac` 1.0 → 0.7 (소총 16.8칸 — 사수 · 공격 이동 대형 탐색도, 기관총 · 저격총 등 무기 `engageFrac` 1.0), 전술 AI engageFrac 0.60~0.68, 반응 거리 밖 총격에 반격 (공격 이동 대형 포함, 피격 중 대기는 다가가지 않음), 진지 점유 명령 (참호 자리 → 엄폐 칸 배정), 전술 AI 방어 기본 = 적 쪽 참호선 → 가옥 · 숲 거점, 기관총 진지, 후퇴 = 다음 참호 · 엄폐선, 예비 엄폐 뒤, 피격 시 엄폐 찾기 (3칸), 공격 엄폐 경유 (가벼운 판), 방어 정면 상한 (참호 없는 정면). `KD_SELFTEST_ONLY=engage`, ENGINE_NOTES '교전 거리 · 엄폐 (engage)', 측정 BATTALION_CAMPAIGN 'engage' (병합 뒤 main 대비: 12~18칸 사격 4~14% → 66~77%, 명중 11~14 → 19~21%, 피격 방어 병사 참호 · 엄폐 17 → 60% (T 2:1) · 7 → 57% (3:1) · 34 → 75% (B); 3:1 공격 8/8 · 2:1 7/8 (main 8/8), 방어측 사격 우위는 회복 안 됨 — 후보 4개 사용자 결정 대기).
N29. ✅ (2026-10-19 브랜치 `arty2` 병합 — 지시서 이름 'N22 포병 기여 · 보병포 · 정찰병', 번호 겹쳐 N29) 上 보병포 = 대대 동행 근접 지원포 (지원 중대 뒤 따라감 · 사격선 3칸 뒤 방열 · 도착 즉시 표적 쪽 방열 · 기관총/포/엄폐/뭉친 적 우선), 직사포 사선 막힘 재배치 (봉천 보병포 첫 사격 120초 원인 = 숲 · 고지 뒤 방열 → 37초), 곡사포 재배치 쿨다운 30→20 · 사거리 0.85→0.75 (C 곡사포 살상 108→126), 정찰병 시야 48 · 피격 엄폐 (중대 정찰조 · 관측반, C 정찰조 생존 14→26/90 · 관측반 7→13/20). 보병포 HE 수치는 감도 시험 잡음 안 → 미적용. `KD_SELFTEST_ONLY=arty2`, ENGINE_NOTES 'N22 포병 기여', 결과 ARTILLERY_EFFECT 'N22' (대대 보병포 기여는 여전히 작음 4.9% — 수치 변경은 사용자 결정).
N31. ✅ (2026-10-19 브랜치 `cavcmd` 병합) 中 기병 지휘: 기병 장교 `officer_cavalry` (장교 + 기병 이동, 그림 cavalry-officer · -fengtian, 미션 기병 중대 6곳 교체) · 기병 분대 부사관 (이미 배정되던 것 명시 + 테스트) · AI 기병 돌격 때 장교는 뒤 10칸 (`cavalry.officerBack`). 측정 TACTICS_OBS 13차 (기병 장교 지휘 389 → 1244 중대-초, 돌격 효과 +2.17 → +3.93). 테스트 `KD_SELFTEST_ONLY=cavcmd`. 미결: 기병 측면 강도.
N31b. (진단 완료 2026-10-19) 미사격 진단 `docs/legacy/NOFIRE_DIAG_2026-10-19.md`: 사거리 안 적이 보이는데 4초 이상 안 쏜 사수-초 80.8%, 최대 원인 = 사선 막힘 55% (철조망 22% = Terrain.cs:24 Walkable=false 가 사선까지 막는 **버그**, 깊은 숲 20%, 고지 7%, 건물 3%), 반응 거리 밖 16%, 돌격 사격 전진 11%. 프로브 `KD_NOFIRE=1 --tactics-obs`. 수정은 미착수.
N36. ✅ (2026-10-19 브랜치 `playercmd` 병합) 섞인 선택 돌격: 하나라도 근접 무기 있으면 `C` 버튼 · 단축키 동작, 돌격 가능한 병사만 돌격 · 나머지 (기관총 · 포 · 수레) 사수 (`World.ChargeMixed`, `Game.CardAvailable`).
N37. ✅ (2026-10-19 `playercmd`) 플레이어 명령 지연 = 선택 인원 곡선 (`balance.json` `command.playerSelSizes/playerSelMul`: 1명 ×0.25 · 분대 ×0.3 · 소대 ×0.5 · 중대 ×0.8 · 대대 이상 ×1), 명령에 `Sel` 기록 → 리플레이 형식 12. AI 명령 ×1 (detcheck 불변). 테스트 `KD_SELFTEST_ONLY=playercmd`.
N38. ✅ (2026-10-19 `playercmd`) 철조망 사선 허용 (`GameMapTerrain.ClearFor`; 물 · 바위 · 집 · 벽은 그대로 막음). B (ai_bn_assault) 시드 1~5 `KD_NOFIRE=1`: 미사격 88.0% → 72.2% (철조망 사유 53~62% → 0), 승리 5/5 → 5/5, 승리 시각 평균 356 → 186초, 공격측 K/E 1.40 → 1.61 (방어측 교환비 0.71 → 0.62, 방어 이점 감소).
N40. ✅ (2026-10-19 브랜치 `crossing` 병합, Snapshot v33) 上 도하 · 병목 돌파 + 역습 교리: 단일 건널 곳 (폭 ≤10, 둘째 건널 곳 없음) 이면 화력 기반 (강둑 칸 = 반응 거리 안 적 맞은편) → 화력전 진척 멈춤 · 병력비 2.5 → 거의 동시 도하 → 교두보 → 계속, 미션 posture `counter` = 고정 사격 → 일제 돌격. `KD_SELFTEST_ONLY=crossing`, ENGINE_NOTES '도하 · 병목 돌파', 측정 BATTALION_CAMPAIGN '도하 · 역습 (crossing)' (다리 1 소총 3:1 0/8 → 4/8, 화력 지원 승리 362 → 186초, 1.5:1 교착 유지, 역습 1.5:1 이미 8/8 → 탈환 141 → 113초). 결정 대기 3개.
N34. ✅ (2026-10-19 브랜치 `human` 병합) 덜 기계적인 전투: 대형·방향 변경 병사별 반응 0~3틱(`command.formReactMaxSec`, 중대 명령 출발 지연 대신), 포마다 고정 발사 간격 배수 0.93~1.07(`artyRateMin/Max`), 5초 넘게 안 쏜 보병 첫 사격 0.2~1.2초×숙련(`firstShot*`) — 첫 일제 폭 0.70→1.47초. `KD_SELFTEST_ONLY=human`, selftest 1313 OK, golden 갱신.
N41. ✅ (2026-10-19 브랜치 `s3a` 병합) 上 전술 AI 3차 3A: S1 기동 계획 (정면 · 측면 · 협공 · 중점, 고정 → 기동 도착 → 돌격 동기, 우회 ≤ 정면 ×2 · 도착 ≤120초) + S14 미션 목표 → 과제 + 미션별 `aiWithdrawAt` · `aiCommitment`, 모두 프로필 키 뒤 (2단계 비교 = 겹침 `korean+stage2`). FormatVersion 31, 골든 `ecb10d9e483ee073 77daaf7d040fe4ee`, selftest 1330 OK (`KD_SELFTEST_ONLY=s3a`). 8시드: 측면 8/8 선택 · T_r1.5 승 6/8 (2단계 5/8) · PIN 협공 승 7/8 (2단계 4/8), 순서 62~75% (목표 80% 미달) · 협공 손실 45% (목표 27% 미달). 결과 · 남은 일 = STAGE3_REVIEW '3A 결과'. 다음 3B.
N39. ✅ (2026-10-19 브랜치 `supp` 병합 — 지시서 N25-supp 'N39 제압 재설계', Snapshot 고정 Pinned) 上 제압 재설계: 근접 탄착 1.5칸 50% · 명중률 ×(1 − 0.5 제압) · 고정 (엎드림 ≥0.6 / <0.4, 후퇴만 이동 · 피격 ×0.6 · 사격 ×0.75) · 장교 제압 후퇴 굴림 (0.3 시작 · 0.6 높음 · 사상 결합 · 경험 ↓ · 부사관 ↑) · 분대 아이콘 고정 · 감소 80 → 25/초 + 4초 안 노려지면 ×0.25 (0.6 → 0.4 = 11초, 사용자 결정) · 무기 제압 소총 60 · 경기관총 48 · 중기관총 36. 결과 교전 중앙 0.00 → 0.23 · p90 0.06 → 0.67 · 고정 15% · 제압 후퇴 9%, 승 · K/E 거의 그대로 (docs/legacy/BATTALION_CAMPAIGN_2026-10-19.md '제압 재설계 결과').
N35. ✅ (2026-10-19 브랜치 `officer` 병합) 장교 교전 회피 · 정찰병 자위 사격: 장교 단독 돌격 차단 (중대 보병 30% 돌격 시만 함께, 돌격 병사 앞줄 뒤) · 표적 찾기 없음 · 자위 사격 (적 4칸 / 중대 근접전) · 정찰병도 적 4칸 안만 (맞으며 멈춘 경우 10칸 반격). 값 balance.json combat.officerAvoid/scoutAvoid. 테스트 `KD_SELFTEST_ONLY=officer`, 측정 TACTICS_OBS 14차 (장교 단독 돌격 0). 부작용: C 지휘없음 시간 증가.
N26b. ✅ (2026-10-19 브랜치 `factions`) 中 마적 세력 `bandit` 유닛 4종 (잡병 대도만 · 소총병 · 기병 · 지휘관) + 세력 제한 일반화 (`factions`) + 그림 대용 (`artAs`) + 시험 미션 `bandit_raid`. 전용 그림이 오면 `unit.bandit_*` 로 교체. (본 N26 로드맵의 첫 단계 — 캠페인 배치는 N27.)
N53b. ✅ (2026-10-19 브랜치 `factions`) 中 공용(무소속) 유닛 틀 `common: true` (모든 세력 편성 허용) + 자리표시자 `common_volunteer_tmp`. 실제 유닛 사양 (간도 한인 의용병 · 몽골 기병대) 은 사용자가 나중에 줌.
N54b. ✅ (2026-10-19 브랜치 `factions`) 中 소총 7종 (ammoType) + 세력별 결정적 분배 (`factionRifles`, 미션 `weapon` 고정) + 한국 구식 보병 `infantry_default_old` (베르단 II). 상세 `docs/WEAPONS_RIFLES.md`. 골든 갱신 (무기 키가 체크섬에 들어감, 시뮬 동일).
N44. ✅ (2026-10-19 브랜치 `tweak1` 병합, Snapshot v34) 中/上 전투 조정 묶음: 대열 정비 화력조 · 지원 제외 + 자기 돌격 중 보류, 피격 엄폐 = 방어 자세 중대만 (`Company.Posture` · `SetPostureCommand`, 전술 AI 과제로 지정), 참호 명중 0.5 (trenchPerTile 2), AI 사격 진지 engageFrac 0.6, 관측자 노출 지점 120초 재진입 금지, 야전포 도착 방열, 보병포 피해 70 · 반경 1.5 · 제압 250 (C 20판: 살상 몫 3.7 → 10.8%, 제거 시 한국 생존 −72 [27], K/E 0.75 → 0.72). `KD_SELFTEST_ONLY=tweak1`, ENGINE_NOTES 'tweak1', 측정 TACTICS_OBS '14차' · ARTILLERY_EFFECT 'N44'. **미해결**: 한국 돌격 후퇴 개선 안 됨 (C 20판 59%, A·B·C 15판 13/13) · 참호 2:1 공격 손실 34 → 46% (5판) — 사용자 확인.
N23 · N55. ✅ (2026-10-19 브랜치 `n23`, 병합 대기) 돌격보병 난사 · 소탕: 카빈 간격 1.1 · 명중 0.25/0.04 · 제압 50, 행진 명중 ×0.8 · 간격 ×1.0 · 속도 ×0.5, 지휘자 없이 걸으며 쏨, 3칸 수류탄 (50%) → 단검, AI · 중대 틀 소탕 (적 너머 공격 이동). main 병합 뒤 동가치 평지 10/16 · 숲 9/16 · 장교 중대 5/8 (전 0), 동수 14~16/16 (hp 130), 단 대대 섞음 3/8 (K/E 0.79, 대조 0.97 — 원인 미조사). 장교 성향 (officers.style): 신참 소위 0.7 · 대위 0.5 · 경험 3단계 대위 0.26 돌격 사격 전진 (아니면 교대 약진), 기병 무모한 정면 0.5 → 0.06. 근거 `docs/legacy/EXCHANGE_TABLE_2026-10-19.md` N23 · `TACTICS_OBS` 15차. 남은 후보 (승인 필요): 돌격 중 제압 증가 감소.

## E. 재개 대기열 완료 항목 (2026-10-18~19 ✅ / ~~)

2. ✅ **전술 AI 2단계** — main 병합 `54e9e28` (934 OK). 남은 일: 밸런스 재측정 → 새 수치 제안 (4번 뒤). 3단계 후보는 `docs/TACTICAL_AI_DESIGN.md` §9.
4h. ✅ (2026-10-18 art4: 행진 사격 walk_fire 에만 `UnitRenderer.DrawMarchFlash` 덧그림, 사격 칸에는 이미 화염이 구워져 있어 중복 없음) **中 총구 화염**: 돌격 사격 중 화염이 계속 번쩍임 → walk_fire 시트에서 화염 제거 (재렌더 때 함께) + 게임에서 사격 사건 때만 화염 오버레이.
4i. ✅ (2026-10-18 art4: `tools/import_kd1920_badges.py`, 테마 키 `icon.badge.<세력>@<크기>`, `UnitSymbols.DrawBadge` — 아이콘 + 세력 색 테두리 + 우하단 병종 기호, 테마에 없으면 NATO 틀) **中 NATO 중대 기호 → 세력 휘장**: 휘장 원본 `game-tools/image/tools/parts/badges.py` + `_drafts/badges/` 5종 → 2D 아이콘으로 뽑아 축소 화면 부대 표시에 사용.
4j. ~~**下 선택 표시**: 녹색 선택 선 최대한 얇게, 교전 중 주황 원은 녹색 선을 대체 (겹치지 않음).~~ ✅ (정리 묶음 2, 2026-10-18 cleanup2 병합)
4k. ~~**中 옛 군복 슬롯 데이터 삭제** (사용자 2026-10-19, wargame 잔재): balance.json `uniforms` (`m41` "M1941 야전복", concealment 0) + 유닛 17개의 `"uniform": "m41"`, `Defs.Uniforms` · `UnitDef.Uniform`, `Detection.cs` 146/154 은폐 보정 훅 (값 0 이라 시뮬 영향 없음 — detcheck 그대로여야 함), `Hud.cs` 525 군복 줄 + `S.UniformLabel`, ObjCodec 확인. `armors` (차량 장갑) 는 유지.~~ ✅ (정리 묶음 2, 2026-10-18 cleanup2 병합)
   - ~~**`combat.targetSpreadJitter` 6 → 5** (사용자 2026-10-19: 4 와 6 의 중간값). 시뮬 변경 → detcheck 골든 갱신.~~ ✅
4m. ~~**中 대포 회전 속도** (정리 묶음에 포함): 이동 중 포 방향 전환이 너무 빠름 (특히 보병포) → 견인포 회전 속도 크게 낮춤.~~ ✅ (정리 묶음 2, 2026-10-18 cleanup2 병합)
4l. ~~**上 참호 자동 점유** (정리 묶음에 포함): 이동 중 참호를 만나면 들어가 사수, 재명령 시 해제 후 이동 (방금 나온 참호에 재점유 금지), 참호 안 보병은 돌격 사격 안 함.~~ ✅ (정리 묶음 2, 2026-10-18 cleanup2 병합)
4g. ~~**上 건설(참호 파기) 기능 제거** (사용자 결정): 개인 참호 · 참호선 명령 · 상태 · UI 카드 · 전술 AI DigIn · 저장 필드 · 테스트 제거, 참호 지형은 지도/미션 배치만, 포격 구덩이 유지. FormatVersion 올림, 골든 갱신. 전술 AI 2단계 합친 뒤 진행 (AI 코드 겹침).~~ ✅ (정리 묶음 2, 2026-10-18 cleanup2 병합)
4b. ~~**下 교전 테스트 N 키 소리**: N 을 누르면 교전 테스트 무음(Sfx.Silent)도 해제 (`src/Game.cs` 216줄 근처).~~ ✅ (정리 묶음 2, 2026-10-18 cleanup2 병합)
4d. ~~**上 유닛 충돌 · 대형 재설계 + 움찔거림** (한 작업): 대규모 전투에서 병력이 비벼짐 → 밀어내기 · 반경 · 공간 분할 재설계, 깃발 대형 (슬롯 · 재정렬 · 빈자리 점검) 점검, 보병 · 기병 이동 중 움찔거림 원인 같이 확정. **어택땅 시 대규모 병력이 한 점으로 몰려 대형 붕괴** → 공격 이동도 대형 슬롯으로 펼쳐 도착. **돌격 사격을 장교 지휘 하 중대 단위 일괄 시작 · 종료로 변경** (분대 단위는 드물게, 지휘자 없으면 개별 돌격 사격 안 함; 돌격 사격 중 거리 ≤3칸이면 자동 돌격 전환; 근접 자동 돌격 반응 거리 chargeAutoRange 1.5 → 1; 병사별 감속 토글이 비벼짐 원인 후보, 사용자 지시).~~ ✅ (2026-10-18 `collision` 병합 — `docs/legacy/COLLISION_ANALYSIS.md`, ENGINE_NOTES '충돌 · 대형 재설계', `KD_SELFTEST_ONLY=collision`, 측정 `--collide scene=battle|march|amove|mfire|loose`. FormatVersion 19. 관찰: AI 대 AI 교전 사상 속도 ≈ −23% → 밸런스 재측정 때 같이 판단)
4f. ~~**下 발밑 세력 색 원 제거** (선택 표시 · 미니맵 유지; 같은 군복끼리 헷갈리는 교전이면 머리띠 표시 — 같은 세력뿐 아니라 군복이 같은 세력끼리도). 결정성 · 1만 기 성능 유지, 성능 측정은 상한 (≤4000기).~~ ✅ (정리 묶음 2, 2026-10-18 cleanup2 병합)
4n. **(완료 2026-10-19, bundle3 병합)** 잔재 정리 · 비전투 어택땅 · 돌격 사격 토글 · 사병 전투 중 진급 제거 — 상세 docs/ENGINE_NOTES.md "묶음 3". (원문: **中 잔재 코드 정리** (Gemini 점검 2026-10-19, grep 으로 존재 확인 — 쓰임 여부는 수정 때 확인): BuildingRenderer `DrawScaffold` · `DrawProgress` · `DrawResource`, GameMap `IsBuildable`, 시험의 `CountBuildings` · `FuelOf` · HQ 참조, `tools/make_ww2_sprites.py` worker · barracks · depot · factory · tank · turret_gun, `tools/make_sfx.py` build_complete, **StandardAi 는 유지 (사용자 결정)**, 나머지 잔재는 **전부 삭제 (사용자 승인 2026-10-19)**. 도시 · 점령지용 BuildingDef · PlaceBuilding 은 유지.
   - ✅ 병합 끝난 브랜치 삭제 완료 (게임 26 · game-tools 22). 안 합친 것: 게임 `balance-obs-tool` (옛 측정 도구, 현재 main 에 더 나은 도구 있음).
4o. ✅ (N2, 2026-10-19 — 위 N2 줄 · ENGINE_NOTES 참조) ~~**上 경기관총 사격 방향 · 스프라이트 정합** (사용자 2026-10-19, 上 에이전트에 맡길 것): 경기관총이 조준하는 방향과 그려지는 스프라이트 방향이 정확히 맞지 않음 (사선 · 사격 방향 불일치가 큼) + 스프라이트 자체 문제 (무릎 + 모래주머니 흉벽 자세의 방향별 총 위치 · 회전). 원인 후보: 8방향 → 5방향 + 좌우 반전 매핑, 거치 시 고정된 몸 방향 vs 표적 방향 (회전 없이 사격), 조준 각도 양자화, 총구 위치 · 화염 오버레이 기준점. 게임 렌더 · 시뮬 (거치 방향 회전 규칙) + game-tools 시트 양쪽 점검.~~

## F. 사용자 결정 (2026-10-19, 반영 완료 · ROADMAP 1-1 절)

### 사용자 결정 (2026-10-19, 반영 완료 · ROADMAP 1-1 절)
케피 띠 고정 진홍 · 말 견인은 곡사포만 · 경기관총 = 무릎 + 모래주머니 흉벽 위 양각대 · 정찰병 카빈 위치 유지 · 구덩이 확률 승인 · 최저 시야 8칸 · 지휘 지연 세부값 승인 · 지도 크기 현행 · 맵 편집기 = 로드맵만 · 사거리 +8 묶음 (소총 24 · 기관총 24 · 카빈 22 · 대전차 20 · 저격 32 · 보병포 32 · 야전포 50 · 중곡사포 70 · FT 27 · 보병 시야 32 · 지도 ×1.5) 완료 · 자동 참호 없음 · 기관총도 80% 접근 · 제압 ≥400 자동 돌격 금지 · 맞받아 돌격 보너스 없음 · 행진 사격 이동 ×0.5 · 도시 그림 8칸.


## G. 옛 기록 (2026-10-18 이전 상태) · 10-19 전체 정지 · 외출 계획 · 다음 세션 적용 (2026-10-19 결정)

## (옛 기록) 상태 (2026-10-18 마감 이전 — 참고용, 위 절이 우선)
### 저장소
| 저장소 | 경로 | 브랜치 · 커밋 | 비고 |
|---|---|---|---|
| 게임 | `coding/korean-division` | main (교전 2차 `c9af1bb` 이후 문서 커밋) | GitHub private, push 는 사용자 명령 시만 |
| 편제 · 대형 (멈춤) | `korean-division/.claude/worktrees/agent-aa00d5967b0268b5e` | `worktree-agent-aa00d5967b0268b5e` `1f4d833` | 설계 `docs/legacy/ORG_FORMATION_PLAN.md`, 교전 2차 다음에 재개 |
| 그림 도구 | `coding/game-tools` | main `ecfc583` (깨끗) | 로컬 git, 태그 `wargame-64x80` |
| 차량 그림 | (머지됨) | `vehicles` 브랜치만 남음 | |
| 그림 출력 | `coding/game-tools/korean-division-image-out/<유닛>/` | — | contact_<유닛>.png 를 사용자가 검수 |

### 진행 중 에이전트 · 남은 대기열
1. **교전 2차 — 완료 `c9af1bb`** (표적 분산 · 제압 · 계급 · 장교 · 착검 돌격 · 대전차소총 · 반응/멍칫 · 기관총병 + 65% 접근 · 명중 0.35/0.06 · 견인포 시야 · 탐색 비용 · 보병 장갑 0 · 보병 사선 · 무음 `--mute`/N 키). FormatVersion 4. 결과 · 미결 = ROADMAP §2-6. **내일 먼저**:
   1. 미해결 테스트 1건: `KD_SELFTEST_ONLY=supply bin/c2/KoreanDivision.exe --selftest` 의 "3발 쏘고 대도시로 가서 채우고 복귀" — 표적이 반격 · 돌격하지 않게 재설계, 사수 보병이 공병을 표적으로 안 잡는 이유 확인 (`IsValidTarget`/`FindTarget` LOS 경로 의심) → 전체 `--selftest` → `--detcheck` 재확인.
   2. 사용자 결정 (§2-6): ① 표적 분산 흔들림 4칸 → 6~8? ② 기관총병도 65% 접근 규칙? ③ 자동 돌격 2칸이 밀집전에서 과다 ④ **교전 템포가 너무 빨라짐** (200 vs 200 81~90초, 1킬당 12발; 1차 5~7분) — 교전 거리 · 곡선 · 돌격 거리 조정. ⑤ 장교 판정 비용. 사용자가 '전투 추가 개편' 예고함.
   3. 근접 그림 · 효과음 미연결, 장교 · 기관총병 전용 그림은 게임 반영 때.

2. **그림 (game-tools main, 마지막 `ecfc583`, 작업 트리 깨끗)** — 완료: WWI 보병 (`66662e1`, `a758910`), 장교 수정 · 날 방향 규칙, 클리핑 2건. 남은 대기열 (순서):
   1. ✅ sword_wave 뒤쪽 궤적 (`7749cd1`), ✅ 들회색→회청색 (`9447906`), ✅ infantry-japanese (`b1e67e4`), ✅ vehicles 머지 (`ec38de4`).
   2. **관통 검사 보정** (`tools/collide.py`, WIP): 팔 캡슐 · 허벅지 허위 양성 → 팔 세그먼트 제외 / 손 근처만 허용, TOL ≈1.5, 깊이 히스토그램으로 임계값 → 실제 관통만 수정 · 보고.
   3. `walk_shoulder` (어깨총 걷기 4프레임, walk 다리 재사용; 게임에서 이동 시 33%).
   4. 기관총병 (양각대 경기관총, 어깨에 메고 → 무릎 → 엎드려 거치, 연사 루프, 해제).
   5. 대전차소총병 (긴 대전차소총 + 양각대, 기관총병과 같은 전개).
   6. `infantry-regular` = 슈탈헬름 보병, 표시명 **철모 보병** (임시).
   - **돌격보병 `infantry-assault`** (sonnet): MP18 형 기관단총 + 아드리안 철모 + 장교보다 조금 짧은 도검 패용; 돌격 · 근접은 도검 (장교 charge/melee 포즈 · 칼집 · 손잡이 재활용 가능하면 재활용). ROADMAP §3.
   - 원칙: 보병은 **일본군 느낌 금지** (japanese-test 제외). @32 가장자리 접촉은 허용.
   - 나중 (로드맵): 들회색을 회청색 쪽으로, 장교 칼 손잡이, 장교 · 보병 군복 색 체계.

3. **차량 그림 — 완료, game-tools main 에 머지됨 (`ec38de4`, 워크트리 제거 · 브랜치 유지)**: tank-ft-female · tank-ft-male · armored_car (차체 192² 9방향+반전, 포탑 160² 피벗 (80,88), `make_vehicle.py`, 코드 `tools/vehicles/`). 남은 일: 사용자 검수 → 수정, 게임 로더에 차량 레이아웃 (9행 + mirror_map, turret.mount, LOD 태그 @96/@48 · @80/@40) 지원. 선택: 포탑 aim 프레임, 차량 초상화.

### 다음 (순서)
교전 2차 마무리 (테스트 1건 · 결정 ①~⑤) → 편제 · 깃발 대형 재개 → 자동 분대 · 임시 부사관 · 빈자리 점검 (+장교 칼 흔들기 33%) → 교전 3차 (대전차소총병 장갑 우선 · 전차 소총 면역 · FT 2종 · armored_car 유닛) → 새 그림 게임 반영 (3벌 시트 · layout json · 메모리 절감 · walk_shoulder 33%).

### ▶ 재개 (2026-10-19, 새 Anthropic 계정 Max) — 5개 실행: 上 방향 변경 (`wip/direction-change`) · 中 그림 합치기 마무리 + 봉천 변형 (game-tools main) · 中 포병 (u5) · 中 러시아 모자 + 러시아 변형 (u7) · 中 지형 A (`kd-terrain-art`). 대기: 上 지형 B (방향 변경 뒤), 中 교전 4차, 中 walk_fire/walk_shoulder, 기병 · 저격수 합치기.

### ⏸ 전체 정지 (사용자 지시 2026-10-19) — 재개용 상태
모든 에이전트 정지. 커밋 안 된 작업은 **WIP 커밋**으로 보존 (main 은 마지막 깨끗한 커밋 그대로):
| 작업 | 위치 · 브랜치 · 커밋 | 상태 |
|---|---|---|
| 上 방향 변경 대작업 | 게임 `wip/direction-change` `cc2390c` (main `afd5aa3` 기준, 20파일) | 진행 중 정지 — 재개 시 이 브랜치에서 계속 (빌드 · 테스트 미확인) |
| 上 지형 B 블렌딩 | `coding/kd-terrain-blend` (`terrain-blend`) | 시작 직후 정지, 변경 없음 |
| 中 지형 A 다듬기 | `coding/kd-terrain-art` (`terrain-art`) | 시작 직후 정지, 변경 없음 |
| 中 그림 합치기 u1/u2/u3 | game-tools main `3cbf58a` (u1·u2·u3 머지 커밋 있음) + 미완 수정 `wip/art-merge-variants` `cb085fa` | 재렌더 · 관통 0 확인 미완 (보고: 브랜치 bases 가 main 의 officer.py 리팩터에 의존) |
| 下 일본 세력 변형 | 위 `wip/art-merge-variants` 에 포함 (officer/heavy-*-japanese.json) | 렌더 미확인 |
| 下 봉천 세력 변형 | — | 파일 없음 (시작 전 정지) |
| 中 포병 3종 | game-tools `units-u5` `bc3457c` (WIP) | 진행 중 정지 |
| 中 러시아 모자 2종 | game-tools `units-u7` `2ad49fd` (WIP) | 진행 중 정지 |
| 기병 u4 · 저격수 u6 | `units-u4` `ff36709`, `units-u6` `f2b040f` | 완료, 미합침 |

### 외출 중 에이전트 지도 · 토큰 최소화 규칙 (2026-10-19 09:00)
| 줄 | 지금 | 다음 (끝나면 순서대로) |
|---|---|---|
| 게임 | 上 방향 변경 대작업 (opus) | 中 교전 4차 (sonnet) → 上 지형 B 경계 블렌딩 (opus, 마지막 · 토큰 부족하면 보류) |
| 그림 main | 中 u1/u2/u3 합치기 · 재렌더 (sonnet) | 中 walk_fire + walk_shoulder (sonnet) → 세력별 외형: 下 색 · 모자 교체 JSON (haiku) + 中 새 모자 2개 (푸라시카 · 부됸놉카, sonnet) → 中 지형 A (sonnet) |
| 그림 워크트리 | ✅ 기병 u4 (`ff36709`, 192×224, 약점 목록은 귀가 후 검토) · 中 포병 u5 (sonnet) · ✅ 저격수 u6 | u4 · u5 끝나면 中 u4/u5/u6 합치기 + 게임 재가져오기 (sonnet) |
- 규칙: 한 줄에 동시 1개 (워크트리 마무리 제외), 단계마다 **새 에이전트 + 짧은 지시문** (문서 경로만 가리킴), 큰 에이전트 이어 쓰기 금지, 긴 로그는 Gemini, 코디네이터는 보고 요약만 · HANDOFF 갱신은 단계 끝날 때 한 줄. 결정 필요 → 멈추고 귀가 후 보고. push 금지.

### 외출 중 실행 계획 (사용자 승인 2026-10-19 09:00~12:00) — 이 순서대로 이어 띄움
- **게임 줄**: ① main 마무리 (깃발 속도 버그 · 남은 검증, 中 진행 중) → ② **上 방향 변경 대작업** (`docs/legacy/PLAN_DIRECTION_CHANGE.md`, opus) → ③ **中 교전 4차** (포병 3종 · 수류탄 (돌격보병 1) · 정규병 · 시위대 HP150 긴 소총 · 봉천 대도 · 중기관총 HP200 · 저격수 · 행진 사격 (보병 + 기관총 차량, 속보 중 불가) · 기병 수치 + 충격 돌격 300/마비 0.4~0.6 · 차량 근접 불가 · 유닛 id 이름 규칙 정리) — ROADMAP 해당 절이 사양.
- **그림 줄**: ① u1/u2/u3 합치기 + 전체 재렌더 · 관통 0 (中 진행 중) → ② 中 `walk_fire` 2프레임 + `walk_shoulder` 4프레임 (보병 계열 전체) → ③ 中 **세력별 외형** (공용 병종 초안 묶음 × 일본 · 봉천 · 러시아 백군 · 러시아 적군, ROADMAP '세력별 외형' 초안; 사용자 귀가 후 검토) → ③b 中 **지형 A 기본 다듬기** (ROADMAP '지형 그래픽 다듬기' A) → (게임 줄, 방향 변경 끝난 뒤) 上 **지형 B 경계 블렌딩** — 원래 ③b 설명: (현재 지형 타일 · 도시 · 바위 · 물 · 길 등을 1920 풍 · 새 유닛 화풍 (8단계 명암, 픽셀아트) 에 맞게; `tools/make_ww2_sprites.py` → kd1920 테마 지형; 지도 코드는 건드리지 않음) → ④ 기병 (u4) · 포병 (u5) · 저격수 (u6) 끝나면 합치고 게임에 재가져오기.
- 멈춤 조건: 결정성 깨짐 2회 실패 / 결정이 필요한 상황 → 그 단계 커밋 후 대기. push 금지.

### 2026-10-19 진행 중 (에이전트 6개) · 후속 대기열
| 등급 | 작업 | 위치 |
|---|---|---|
| 中 | 그림 미세조정값 JSON 분리 (`image/tune/*.json`, 렌더 결과 픽셀 동일 확인) | game-tools main |
| 中 | armored_car 4륜 + 기관총 fire_2 연기 제거 | game-tools 워크트리 `game-tools-veh2` (`vehicles2`) |
| 上 | 편제 · 깃발 대형 (main 병합 후 구현) | 게임 워크트리 `agent-aa00d5967b0268b5e` |
| 上 | 새 그림 게임 반영 (3 LOD · layout json · 차량 16방향+포탑 · R8 팀층 · walk_shoulder 33%) | 게임 워크트리 (자동 격리) |
| 中 | 교전 3차 데이터 (대전차 장갑 우선 · 차량 소화기 면역 · FT 2종 · armored_car · 돌격보병 기관단총 25/×3.5/⅓) | 게임 main |
| 中 | 스태미나 · 속보 · 순찰 제거 | 게임 워크트리 (자동 격리) |
**후속 (JSON 분리 끝난 뒤, game-tools)**: 上 보병 겹침 오류 일괄 수정 (관통 검사 보정) → 中 walk_shoulder; 그리고 워크트리에서 中 새 유닛 그림 — 철모 보병 (슈탈헬름) 재작업 · 일본군 군모 재작업 · 돌격보병 · 기관총병 · 대전차소총병.
**합치기 (끝나는 대로)**: `vehicles2` → game-tools main; 게임 워크트리 3개 → main (FormatVersion 충돌 정리), 그 뒤 전체 selftest · detcheck.
**下 완료**: 장교 바지 줄 (`7cc0c0a`), sword_wave 고정 (`59b8358`), infantry-default `_doc` 수정, **game-tools GitHub 저장소 생성 · 업로드** (`FailingVillain/game-tools`, private — 이후 push 도 사용자 명령 시만).
**上 1 (방향 변경 대작업)**: 사용자가 2시간 자리 비울 때 진행 — 실행 계획 `docs/legacy/PLAN_DIRECTION_CHANGE.md` ("1번 진행" 명령 시 그대로 실행).

### 다음 세션 시작 시 적용 (2026-10-19 결정)
- **보병 겹침 오류 (개머리판이 몸에 끼어 보임 등) 를 opus 5.5 로 해결** — 관통 검사 도구 보정 (`tools/collide.py`: 팔 캡슐 · 허벅지 허위 양성 제외, 손 근처만 허용) 후 모든 보병 · 장교 프레임 점검 · 수정.
- **철모 보병 (`infantry-regular`, 슈탈헬름) 을 sonnet 으로 재작업**: haiku 1차 (`9c95eef`, `image/tools/parts/helmets.py` `stahlhelm`) 결과 불만족 (사용자 중단). M1916 슈탈헬름 — 깊은 둥근 철모 (귀 · 목까지), 바깥으로 벌어진 목 가리개, 짧은 앞챙, 양옆 환기 돌기, 팀색 아래 띠, 휘장 없음. 128 · 64 에서 특히 E (옆) 방향 실루엣이 확실히 읽히게 (1차는 E 방향 띠 · 돌기가 약했음).
- **일본군 테스트 보병 군모를 sonnet 으로 재작업** (haiku 시도 `f929eda` — 변경이 7줄뿐이고 검수 시트가 커밋보다 먼저 렌더돼 사용자 눈에 변화가 안 보였음. 반드시 수정 후 다시 렌더하고 이전 모자와 실루엣 차이를 수치로 확인): 실제 1차대전기 일본군 45식 군모 — 평평한 둥근 윗면 · 짧은 원통 옆면 · 띠 (팀색) · 작은 가죽 챙 · 턱끈, 휘장 없음. `image/tools/parts/headgear.py` `japanese_cap`, 렌더 `python image/make_unit.py infantry-japanese`.
- 교전 템포는 엄폐 · 참호 · 새 보급 이후 재측정 (ROADMAP §2-7). 음성은 나중에 (파일 제거됨).
- **그림 미세 조정값을 JSON 으로 분리** (자세 각도 · 칼/소총 위치 · 모자 크기 · 재질 색 램프 등 → `units/*.json` · `anims/*.json` 또는 `tune/*.json`), 코드 수정 없이 haiku 가 숫자만 바꾸게. (편집 도구는 그다음 후보)
- **Fable 사용 금지** (사용자가 다시 허가할 때까지). 어려운 작업 = opus, 보통 = sonnet, 쉬움 = haiku.
- 방향 변경 작업 (자원 · 생산 · 건물 · 공병 제거 + 새 보급 + 엄폐 + 개인 참호) — ROADMAP §0-1.


## H. 2026-10-19 밤 · rankui · snrain · forest · ranks 항목

### 2026-10-19 밤: 밸런스 Part B 적용
engageRangeFrac 1.0 · 근위 hp 125 · 맥심 hp 260 · 징집병 0.9 / 정예 1.1 · 돌격보병 hp 130 / 제압저항 0.5 · 공격 이동 참호 불포획 · 흔들림 제거 · 반응 지연 ×0.7. 골든 갱신 (b50ceec0…). 테스트 조정: 기병 테스트 seed 2, 혼전 경계 80%. 상세 docs/ROADMAP.md 끝 "밸런스 결정".

### 2026-10-19: rankui (진급 상한 중령 + 병종 이미지 마커)
`officers.maxPromotionRank` 5 (골든 불변), 축소 중대 기호 = 휘장 + 병종 이미지, 중대 카드 12px 병종 이미지. 새 selftest 7개 (1248 OK). 세력별 계급 이름 문서 대기 중 (RankNames 표에 추가).
### 2026-10-19 snrain: 저격 간격 8초 (7~9) + 비 이동 감소 제거 (rain.move 1.0). GOLDEN ae5fd2de72ccde9e 8f2cbcbf61e4e7ab, selftest 1241 OK 유지. 상세 EXCHANGE_TABLE · TACTICS_OBS 끝.

### 2026-10-19: forest (숲 전투 — 정찰조 숲 탐색 + 은폐 약화)
`Scouts.cs BuildProbe` (앞 24칸 숲 · 25초 조용 · 적 안 보임 → 정찰조가 깊은 숲도 들어감, 상태 없음 = FormatVersion 30 그대로) + balance `terrain` forestSeeDepth 2→3 · forestHit 0.7→0.8 · forestSuppress 0.8→0.85, `scoutAi.forestProbe*`. 측정 8시드 (BATTALION_CAMPAIGN '숲 전투'): 숲 조우전 교착 7/8 → 0~4/8 (탐색 효과), 종대 매복은 미결 그대로 (후퇴 뒤 방어가 안 쫓음 — 시야 교착이 아님), 깊이 3 은 종대 매복 방어를 ×1.9 로 키움 → 사용자 판단 대기 (깊이 2 복원 + 탐색만이 가장 깨끗). 테스트 `KD_SELFTEST_ONLY=forest`. → 결정 2026-10-20: 숲 시야 깊이 3 유지.
### 2026-10-19: ranks (세력별 계급 표)
6개 세력 계급 이름 (docs/RANKS_BY_FACTION.txt) 을 ranks.json 에 적용, 미사용 계급 null + Snap/Next, 장성 표시 목록. 시뮬 · 골든 불변. 자세히 ENGINE_NOTES '세력별 계급 표'.


## (옮김 2026-10-20 밤) 옛 현재 상태 · 밤샘 파이프라인 · 대기열
## ⏸ 현재 상태 (2026-10-20 07:30 갱신 — 위 "밤샘 결과" 가 최신, 아래 표는 00:30 기준; 옛 저장 — 사용자 "12시 30분까지, 못 하면 내일")
main `91b29da` (FormatVersion 35, replay 13, selftest 1515 OK — CLAUDE.md 개수 줄 갱신 아직). 게임 dist 는 마지막 병합까지 반영. game-tools main `01c8cff` (N56/56b/57 재렌더 커밋, 출력 폴더 미추적). push 안 함.
에이전트 규칙: `tools/agent/AGENT_RULES.md` + 게이트 `tools/agent/heavy_gate.ps1` (에이전트 프롬프트에 이 파일을 읽으라고 지시): 동시 에이전트 6 (上≤2 · 中≤3), 무거운 작업 4 (게이트 스크립트), 머지는 코디네이터가 순서대로 (에이전트는 merge 금지), HANDOFF N 번호는 코디네이터 지정, CLAUDE.md 개수 줄은 에이전트가 안 고침, 보고 ≤15줄.
규칙 추가 (사용자 승인 2026-10-20): 브랜치는 만든 날 main 에 합친다. 측정이 애매하면 JSON 스위치 (기본 끔) 뒤에 넣어 합치고 나중에 결정.

### WIP 브랜치 (워크트리 `coding/kd-develop/kd-<이름>`, 모두 미병합)
| 브랜치 | 커밋 | 남은 일 |
|---|---|---|
| `lmg` (N64 LMG 중대 편제 + 너프) | `7f35c9b` | 한국 중대 3정 · 봉천 쇼샤 2정 (속도 ×0.7), 일본 LMG 막음. 너프 후 동수 32:1→10.7:1, 동가 53:1→6:1. 카빈 탄 120 복구. 남음: tactics-obs, ENGINE_NOTES, selftest FAIL 1건 ("AI 경기 424² 16인" 전차 끼임 불변식 — main 도 실패하는지 확인), 러시아 LMG 비율 |
| `n50` (대형 재구성 · 간격) | `fcb3b46` | a~e 구현 + 회전 제거 (90° 전환 걷는 거리 10.8→8.2, 7→5.5초). **병합 금지**: 한국 K/E 악화 B 0.61→0.78, C 0.65→0.76 (C 돌격 7/7 후퇴). 남음: 다섯 변경 중 원인 분리 (`docs/n50_raw/`), 수치 조정 (결정 필요 시 사용자), 전체 selftest · detcheck --update · snaptest · 병합. 그다음 N33+N51 떨림 |
- N33/N51 떨림 점검: 대폭 개선 (amove 접촉 역전/s 10.4→2.8, 가속/속도 0.95→0.39, battle 역전 2.3-3.5→1.4), 잔여 있음 (접촉 구간 겹침 ~0.8/유닛, 정체 5~7천/10초) - 추가 수정은 별도 작업 제안 (2026-10-20, main 38b961a)
| `ammo` (N76 탄약) | `1f4a573` | AmmoObs 측정기 (tactics-obs 연결), balance.json: regenDelay 30 · regenPct 0.0015 · 마차 ×4 · 도시 ×3, 한도 소총 16 · 카빈 12 · 돌격카빈 20 · LMG 140 · HMG 320 · 권총 12 · 저격 20 (약 2판분), 수류탄은 도시 · 마차 근처만 회복, 분대 아이콘 LowAmmo (<30%). 남음: selftest 6 FAIL (체크섬 1 → detcheck --update, 옛 탄약값 테스트 5건 고침), detcheck 8변형, uifuzz 30, tactics-obs A/B/C 시드 1~3 재측정 (소진 빈도 · 시점), HANDOFF N76 줄. AI 탄약 부족 대응은 3B |
| `charge` (N46 · N47 · N57) | `d39a14a` | 빌드만 통과, 테스트 0. N46 분대 따라 쏘기 (`squadFollowFireSec` 2초, 분대 안만) 완료. N47 행진 사격→돌격 3→5칸 + ai412 돌격 허가 게이트 완료. 남음 N57 전부: `Unit.BayonetFixed` + 타이머 (저장 · 체크섬 · Audit, FV 36), 착검 · 칼 뽑기 시간 JSON, N37 감소에서 제외, `fix_bayonet` 애니 연결. 검증: `KD_SELFTEST_ONLY=charge` 작성, 전체 selftest, detcheck --update + 8변형, snaptest, tactics-obs A/B/C 1~5. 문서: HANDOFF N46/N47/N57 줄, tactics.json 설명 "행진 사격 3칸 돌격은 게이트 없음" 수정. 상세 = 커밋 메시지 |
| 재렌더 (game-tools) | `01c8cff` + 게임 `7f13870` (PNG) | 21개 중 20개 완료 (보병 14 · LMG 6), 관통 0. N56 엎드림 조준 (뺨 붙임) · N56b LMG 총구 뒤 운반 · 일본 휘장 빨강 제거 · fix_bayonet (infantry.json 만, 봉천 · 저격 계열 없음). 남음: `heavy-machinegunner-russian-white` 1개 렌더, `python tools/import_kd1920.py` (아직 게임에 반영 안 함), 접촉 시트 `docs/art_review/rerender_batch.png` 사용자 확인. 진행 = `game-tools/image/RERENDER_PROGRESS.txt` |

### 사용자 결정 필요
- N47: 돌격 예비 타이머 시작점 = 전술 틀 국면 틱 (틀 없으면 공격 이동 명령 틱) — 에이전트 제안.
- N57: 착검 멈춤을 명령 돌격에만 vs 모든 돌격 (에이전트 계획 = 모든 돌격).
- n50 K/E 악화 원인 나오면 수치 선택. → 결정 2026-10-20: n50 브랜치 삭제 (아이디어는 F1 에 흡수), 수치 선택 없음.
- S1~S5 (아래 단기 목록), 특히 S1 돌격 후퇴율은 제압 재설계 뒤 판단.

- 결정 2026-10-20: n50 브랜치 삭제 (아이디어는 F1 에 흡수). Gemini TTS 기각 · 제거.

### 병합 순서 (제안)
lmg → ammo → charge → n50 (각각 마지막에 main 한 번 병합 → 전체 selftest → detcheck --update → 코디네이터가 --no-ff). 병합 후 dist 재발행 (`publish.ps1`, dist 실행 중이 아닐 때).

### 🌙 밤샘 파이프라인 (사용자 승인 2026-10-20 01:24, **07:30 종료**, 에이전트 동시 ≤4, opus 신규 금지 · 주간 사용량 72%)
사용량 상한 (주간 90% 대비, 추정): 밤샘 하위 에이전트 가중 합계 1.2M (opus ×1.7 · sonnet ×1 · haiku ×0.3, 01:24 이후 시작분) — 0.9M 넘으면 새 작업 금지, 1.2M 이면 전부 WIP 정지. 사용량 한도 메시지가 뜨면 즉시 정지.
매 정각 점검: 토큰 낭비 (멈춘 · 반복 에이전트) · 빈 자리에 다음 작업 올리기. 실패 시 상위 등급 올리지 않고 멈춤 + 보고.
1. 진행 중: lmg (中) · ammo (中) · charge (上, N57 스위치 기본 = 명령 돌격만 · N47 = 전술 틀 국면 틱) · n77 (中).
2. 병합 (下, 충돌 시 中): lmg → ammo → charge → n77, 각각 전체 selftest + detcheck → main --no-ff. 깨지면 남기고 보고. 끝에 publish.ps1.
3. 병합 뒤 순서: ① N78 '총기병'→'기병' 표시 이름 (下, 타찬카 제외) ② S2~S4 원인 측정 보고 (中, 코드 수정 없음, 시드 3) ③ n50 다섯 변경 원인 분리 측정 (中, 수치 결정 · 병합은 사용자) ④ S5 문서 정리 (下, 병합 다 끝난 뒤, kd 폴더 이동 제외).
제외: N73 · N59/60/65 (上), 타찬카 · 새 유닛 (렌더), N33+N51 (n50 뒤).
07:30 에 이 절 아래에 결과 한 줄씩 기록 + 커밋.

#### 밤샘 결과 (2026-10-20 07:30)
- main `40fcba0`+: lmg(N64) · n77 · n78 · charge(N46/47/57, FV36) · ammo(N76, LMG 탄 80 · 쇼샤도 80 — 확인 필요) 병합, selftest 1634 OK, dist 재발행.
- 미병합 · 병합 준비 끝: `jpinf` (N80 일본 보병 — 철모 효과 없음 결정 필요) · `s4`→`f4` (기병 돌격 꺾임 + 돌격 조건 강화; 소총30:기병30 = 소총1 · 기병2 · 무7, 카빈 · 소총 AI 결정 필요) · `s2` (S2 소형 중대 부부사관, 기병 공백 226→5 · S3 [관측반] 줄) · `n46` (N46b 행진 따라 쏘기, A/B 손실 늘고 C 줄어듦 — 결정 필요) · `f5` (말발굽 음량).
- 보류: `n50` (승인 수치 적용 e36d592, B 0.60 ✅ · C 0.83 ❌) → F1~F3 출발점으로 쓸지 결정. → 결정 2026-10-20: n50 삭제 (F1 에 흡수).
- 새 문제: 소총 탄 16 → AI 도하 화력 단계에서 탄 소진 (CrossingSelfTest 만 무한 탄으로 고정). 기병 officerBack 10 (장교 37~80초 전사).
- 해결 목록 F1~F5 승인 (`docs/ISSUES_2026-10-20.md`): F1~F3 대형 강박 = 上, n50 판단 뒤 시작. → 결정 2026-10-20: n50 판단 끝 (삭제) → F1 시작.
- 그림: 엎드려쏴 짧은 소총 (game-tools c5a168b, 시트 docs/art_review/prone_fix.png) → 승인 시 전체 재렌더 + import.
- 음성: Gemini TTS (판매 게임 사용 가능 — 유료 티어), 시험 생성 미착수. 웹 검색은 `hub ask gemini-flash -w`. → 결정 2026-10-20: Gemini TTS 기각 · 제거.
- soak (`tools/agent/soak.ps1`): 미션 979 + 야전 1065판, 예외 0, 미션 64판 시간 초과 (yalu_crossing · artillery_spotters), 야전 무승부 35%. 자료 docs/soak/2026-10-20 (미커밋). → 결정 2026-10-20: 후퇴 · 재정비 반복 = 미션 설계 때.
- 사용량: 밤샘 가중 합계 약 1.5M (정지 기준 1.2M 초과로 04시 이후 새 작업 중단).

### 대기열 (순서)
1. 위 WIP 4개 마무리 · 병합. 2. N77 미션 (테스트 미션 추가, yalu_crossing 일본 보병으로, AI-vs-AI 관전을 미션으로). 3. N33+N51 떨림 (n50 뒤). 4. 단기 S1~S5. 5. 참호 돌파 묶음 (N59 연막 · 철조망 절단 · N60 경박격포 · N65 연막 수류탄). 6. 기병 하마 사격 + 공통 유닛 (N61/N63/N72) · 새 유닛 (N68). 7. 사격 통제 · 명령 대기열 · LOS 표시 (N73~75). 8. N32 참호 돌격 교리. 9. N78 총기병→기병 이름 · 한국 타찬카. 10. N79 승인 항목. 11. 3B (예비 저탄약 교대 포함) · 3C · 3D + 1만 부하. 12. 지도 편집기. 13. 문서 · kd 폴더 정리, CLAUDE.md 개수 1515 반영.
미추적 파일: 루트 `계급체계 초안.txt` · `소총 초안.txt` (사용자 초안 — 커밋 여부 묻기).

---

- 2026-10-19 완료 요약 → `docs/inherited/HANDOFF_ARCHIVE_2026-10.md` §A
> 2026-10-18 일시 정지 상태 (저장소 상태 · 운영 규칙) → 아카이브 §B
