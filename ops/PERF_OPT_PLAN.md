# PERF_OPT_PLAN - 성능 최적화 계획 (작성 2026-10-22, sim 에이전트 Opus, 계획만 · 코드 변경 없음)
> 근거: ops/PERF_BASELINE_2026-10-22.md + 이번 프로파일 (main 93bbc8e, 로컬 계측 빌드 `kd-develop/kd-perfplan` bin/perfplan, 커밋 안 함). 동시 부하 있음 · 단일 실행 = 잡음 큼 (E10000 avg 같은 빌드에서 15.0 / 8.8 ms).
> **원칙: 아래 T50a~T50d 는 전부 '순수 리팩터' — detcheck GOLDEN 8변형이 바이트 단위로 같아야 병합 가능. 같지 않으면 버그로 보고 정지(골든 재생성 금지).** 행동이 바뀌는 안은 §5 에 따로(사용자 결정 필요).

## 1. 측정 결과 (계측 빌드, 틱당)
| 항목 | E 5000 | E 10000 | C 10000 | A 10000 |
|---|---|---|---|---|
| FindTarget ms/틱 (구간 합) | 2.50 | **10.47** (틱 15.0 의 70%) | 0.34 | 0.36 |
| 호출/틱 (TryAcquire WorldCombat.cs:331 비중) | 38 (31) | 72 (62, 168 µs/호출) | 216 (183, 1.8 µs) | 286 (16) |
| 순회 유닛/호출 · 반경 안 적 | 2500 · 2350 | **6100 · 5600** | 14 · 8 | 10 · 6 |
| 최선 갱신(CanSee 도달)/호출 | 132 | **270** | 4 | 1 |
| HasFiringLine 호출/틱 · 실패율 · ms | 4897 · 72% · 0.31 | 16782 · 77% · 1.05 | 914 · 97% · 0.06 | 288 · 52% · 0.03 |
| Separate ms/틱 | 1.21 | 2.88 | 2.30 | 0.28 |
| Fog ms/틱 (그중 유닛 Stamp 루프) | 0.13 | 0.25 (0.16) | 0.17 | **0.42 (0.37)** |

**FindTarget 원인**: 공간 해시(칸 해시 + 8×8 거친 마스크)는 이미 있다. 난전에서는 거친 마스크가 아무것도 걸러내지 못하고, 반경(평균 25칸, 상한 32) 안의 **모든 적 후보를 행 우선 순서로** 점수 매긴다. 순서가 거리순이 아니라서 '지금까지 최선'이 270번 갱신되고, 그때마다 CanSee + 사선 검사가 돈다(사선 77% 실패). 후보 순회(EdgeDistance sqrt · IsHostile · PairNoise)가 약 9 ms, 사선 1 ms, CanSee 0.3 ms. p99 스파이크는 표적이 한꺼번에 죽은 뒤 재탐색이 몰리는 틱. LOS/ClearFor 는 주원인이 아님.
**Fog 원인**: 셰도캐스팅 계산(Compute)은 0.013 ms/틱으로 무시 가능(메모 적중 96%). 비용은 Stamp 565회/틱 × 0.65 µs = 메모 run 마다 행 단위 Array.Fill(Bs=2 → 블록 1행 = 타일 2행) 호출 부담. Decay·동맹 합침·Remember 는 각 0.01 ms 미만.

## 2. 메모리 (+50~78 MB/h) 판정
- 1시간 B 5000 재현, 21600틱마다 **강제 GC 뒤 살아 있는 힙**: 25.5 → 44.1 → 32.4 → 36.5 → 37.2 → 37.8 → 38.1 → 38.5 → 38.8 → 39.4 MB. 기준선의 '+76 MB' 는 `GC.GetTotalMemory(false)` (수거 전 쓰레기 포함) — **측정 방식 착시**가 대부분. 선형 누수 없음.
- 실제로 자라는 것: ① `TerrainVision.memo` (FogVision.cs) 3.7k → 34k 항목 → 상한 40000 에서 통째 Clear (톱니, 항목당 ~400 B → 최대 ~16 MB) — 유한. ② `World.CommandLog` 선형 (~2000/6분, 리플레이 기록이라 의도된 성장; 스트레스의 Reorder 가 인위적으로 명령을 많이 냄). ③ `Stats.KillSources` (Combat3.cs:188 '[측정 전용]' 인데 항상 기록, 10초 구간 문자열 키 — 사살 수에 비례, 1시간 수백~수천 항목) ④ nav.fields · routes 캐시는 ~3160 · ~700 에서 포화.
- 결론: 누수 수정 카드는 불필요. T50d 에서 (a) 측정을 '강제 GC 뒤 live' 로 바꾸고, (b) KillSources 를 측정 플래그 뒤로 (체크섬·스냅샷에는 없음 확인, BalanceTool.cs 가 읽으므로 그 도구에서는 켬), (c) memo 는 Clear 대신 그대로 두되 상한을 문서화 — 행동 불변.

## 3. 최적화 순위 (기대 효과 · 골든 위험 · 크기 · 핫스팟)
| 순 | 카드 | 내용 | 기대 효과 (추정) | 골든 | 크기 | 핫스팟 / 병합 충돌 |
|---|---|---|---|---|---|---|
| 1 | T50a | FindTarget **거리 고리순 탐색 + 하한 가지치기**, 같은 점수는 원래 순회 순서 키 (칸번호 c, 칸 안 위치 k) 로 판정 | E10000 FindTarget 10.5 → ~1–1.5 ms, 틱 15 → ~6 ms, p99 52 → ~15–20 ms. C/A 는 변화 작음 | 동일해야 함 (아래 증명) | M | WorldCombat.cs 1줄 + 새 파일 TargetSearch.cs. T30b(FindTrainTarget) · T10/N59(SmokeBlocks) 가 같은 함수를 고침 → **0.2 병합 후** |
| 2 | T50b | FindTarget 잔여 미세: 세력별 적대표 (bit&31 가 아닌 정확한 bool[]), jitter 전 기본 점수로 먼저 비교(PairNoise 생략), 건물 루프를 해시/상자 컷 | 남은 FindTarget 의 20–40%, C10000 건물 순회 24k/틱 제거 | 동일해야 함 | S | T50a 와 같은 파일 (직렬) |
| 3 | T50c | Fog: 지형 시야 켜짐(tv) 경로에서 **블록 격자(212²)에 찍고 세력 갱신 끝에 한 번 타일 격자로 확대** (grid[i] = 블록 보임 ? 2 : Min(grid[i],1) = 지금의 Decay+Stamp 와 같은 식) | A10000 Fog 0.42 → ~0.2 ms (목표 0.3 달성) | 동일해야 함 (격자 바이트 동일) | M | FogOfWar.cs · FogVision.cs: T10(조명탄 Stamp) · T30b(열차 객차 Stamp) · N59(연막 StampSmoky, 타일 단위) → **0.2 병합 후**, 연막/조명탄/열차 Stamp 는 확대 뒤 타일 격자에 그대로 |
| 4 | T50d | 측정 도구: stress 에 `KD_MEMPROBE` (강제 GC live 표본) · FindTarget 카운터 상시 훅(Profile 일 때만) · 3시드 반복 표; KillSources 플래그 | 판정 신뢰도 (잡음 2배 → 비교 가능) | 동일 (측정 전용) | S | StressTest.cs, Combat3.cs 1줄 |
| 5 | T50e | Separate 미세 (E10000 2.9 ms, C10000 2.3 ms): 행 단위 빈 칸 건너뛰기(cellMask/거친 마스크), sep 배열 채우기 루프 정리. **쌍 순서(부동소수 누적 순서) 절대 불변** | 15–30% | 동일해야 함 (순서 바뀌면 깨짐 — 위험 중) | S–M | World.cs (T30a/T30b 접촉) → 병합 후, T50a 측정 뒤 필요할 때만 |

### T50a 동일성 근거 (구현자가 지켜야 할 것)
- 지금 결과 = '모든 유효 후보 중 (score, 순회순서) 사전순 최소'. CanSee · SeenBySpotter · HasFiringLine 은 best 와 무관한 순수 필터(부작용 없음 확인: Detected 는 읽기만, HasFiringLine 의 EnsureTerrain 은 지연 생성뿐). 따라서 순회 순서를 바꿔도 **키 = (score, c, k) 사전순 최소**를 고르면 결과가 같다. `score >= bestScore` 대신 `score > best || (score == best && key > bestKey)` 로 거르고, 필터는 그 뒤에.
- 하한: 고리 r (중심 칸 기준 체비셰프) 의 모든 점까지 거리 ≥ r−1, EdgeDistance ≥ 그 값 − 최대 유닛 반경 − 0.01. 음수 보너스 합 maxNeg = 1000 × (threat + prefer≠null + preferOfficer). **하한 − maxNeg > bestScore (엄격)** 일 때만 멈춤(같으면 계속 — 동점이 앞 순서일 수 있음). jitter·+100·+5000 은 양수라 하한에 넣지 않음.
- 칸 집합은 원래 상자 [x0,x1]×[y0,y1] (반경+2) 밖으로 나가지 않는다. 점수 식은 원본과 같은 순서의 float 연산(복사), FMA/재배열 금지.
- 시험: 새 묶음 `KD_SELFTEST_ONLY=findtarget` — 무작위 월드 몇 개(난전 · 동맹 · 은폐 · 철조망/고지 사선 · 방열 사계 · threat/prefer · avoid · fpl · 동점 배치)에서 옛 구현(시험 전용 사본 FindTargetLinear)과 10만 질의 결과 동일 + detcheck 8변형 GOLDEN 동일 + snaptest.

## 4. 실행 순서 (0.2 병합 라인이 끝난 뒤)
1. merge/0.2 · merge/0.2-ui 가 main 에 들어가고 release 가 GOLDEN 을 확정한 뒤 시작 (지금 FindTarget · Fog 를 고치면 T30b · T10 · N59 와 3중 충돌).
2. T50d (측정 도구) → 새 main 에서 기준선 3시드 (E/C/A 5000·10000, B 1시간 live) = 이후 비교 기준.
3. T50a → reviewer → 측정(T50d 도구) → 리허설 → 병합.
4. T50c (T50a 와 파일이 달라 병렬 가능, 上 동시 2 이내).
5. T50b (T50a 위 스택).
6. T50e 는 T50a·b 뒤 E10000 에서 Separate 가 여전히 1순위일 때만.
- 각 카드: 빌드 · detcheck(갱신 없이 GOLDEN 비교) · 자기 묶음 · snaptest. GOLDEN 이 달라지면 wip-ready 금지.

## 5. 행동이 바뀌는 안 (골든 바뀜 — 사용자 결정 전 발주 금지)
- 표적 캐시(틱 기반 무효화: 같은 칸·같은 세력 질의 결과 재사용) — 질의마다 사격자 Id 별 jitter·사선 출발점이 달라 순수로는 불가, 공유하면 표적 분포가 바뀜.
- AcquireRadiusCap 32 → 축소, acquireInterval 증가, 난전 시 재탐색 간격 적응형. FindTarget 를 줄이지만 교전 행동이 바뀜 (밸런스 후순위 원칙에 따라 0.3 이후).
- memo 를 LRU 로 (현재 Clear) — 순수 캐시라 결과는 같지만 효과가 작아 보류.

## 6. 카드 초안 (각 ≤150k 토큰, 역할 sim, 0.2 병합 후)
**T50a FindTarget 고리순 탐색** | sim | D上 H2 (WorldCombat.cs) R3 M | 선행: 0.2 병합 · T50d | 만질 파일: 새 src/Core/TargetSearch.cs, WorldCombat.cs FindTarget 본문 위임 1곳, 새 src/Tests/FindTargetSelfTest.cs (+SelfTest 그룹 등록 1줄). 범위 밖: 점수 규칙 · 반경 · 간격 변경. 수용: GOLDEN 8변형 동일 · findtarget 묶음 동일성 10만 질의 0 불일치 · E10000 FindTarget ms/틱 −70% 이상 (3시드 중앙값) · C/A 악화 없음(+5% 이내). 측정: `--stress EC k=5000,10000 ticks=600` 3시드.
**T50b FindTarget 미세** | sim | D中 H2 R2 S | 선행 T50a (스택) | 같은 파일. 수용: GOLDEN 동일 · findtarget 묶음 통과 · T50a 대비 FindTarget −20% 이상 또는 C10000 건물 순회 0 근처.
**T50c Fog 블록 격자** | sim | D上 H2 (FogOfWar.cs · FogVision.cs) R2 M | 선행: 0.2 병합 (T10 조명탄 · T30b 열차 · N59 연막 Stamp 들어온 뒤) | 수용: GOLDEN 동일 · 새 묶음 `fogblock` (옛 경로와 own 격자 바이트 비교: 지형 시야 켬/끔 · 동맹 · 고지 관측자 · 지도 가장자리 · 연막 · 조명탄) · snaptest · A10000 Fog ≤0.30 ms/틱 (3시드 중앙값). stampEpoch 저장값 갱신 방식 유지(스냅샷 바이트 동일).
**T50d 측정 도구 + 메모리 판정** | sim(또는 feature) | D中 H1 R1 S | 선행 없음 (StressTest.cs 는 병합 충돌 적음 — 0.2 와 병행 가능, Combat3.cs KillSources 한 줄만 병합 후) | 수용: stress 출력에 live 힙 표본 · FindTarget 카운터 · `seeds=1,2,3` 중앙값 표; GOLDEN 동일; KillSources 가 체크섬/저장에 없음을 확인한 경우에만 플래그화.
**T50e Separate 미세 (조건부)** | sim | D上 H2 (World.cs) R3 S | 선행 T50a·b 측정 결과 | 수용: GOLDEN 동일 (쌍 순서 불변) · E10000 Separate −15% 이상.

## 7. 재현 메모
계측은 `kd-develop/kd-perfplan` (분리 HEAD 93bbc8e) 의 미커밋 변경: src/Core/PerfProbe.cs, WorldCombat.cs FindTarget 카운터 · 호출 위치(CallerLineNumber), FogOfWar.Run 구간, FogVision Compute 시간, StressTest `[probe]`/`[fogprobe]`/`[mem]` 출력. T50d 가 이 방식을 정식화한다(호출 위치 문자열 사전은 측정 전용, 시뮬 상태 아님).
