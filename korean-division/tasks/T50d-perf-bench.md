# T50d 성능 벤치 하네스 (측정 도구)
- T50d | feature | D中 H1 (R1 S)
- 선행 없음 | 후행 T50a / T50b / T50c (이 도구로 증명)
- 만질 파일: 새 src/Tests/PerfBench.cs, 새 src/Core/PerfProbe.cs, StressTest.cs 1줄(`bench` 분기), 새 tools/perf_compare.ps1 · perf_overnight.ps1, tools/perf/baseline_main*.json. 공유 시뮬 파일(WorldCombat/FogOfWar/Combat3) 무수정.
- 브랜치 tools/perf-bench (kd-develop/kd-perf-bench) | 상태: wip-ready
## 사용
- 벤치: `KoreanDivision.exe --stress bench scenes=ACE k=1000,5000,10000 seeds=1,2,3 repeats=1 ticks=600 out=x.json label=이름` (JIT 워밍업 1회 버림). 실행마다 mean/p50/p95/p99/max, 구간 ms/틱(World.Prof: FindTarget=[11], Fog=[8], Separate=[4]...), 할당 KB, 끝 체크섬. 요약 = 장면+K 중앙값. 같은 장면/K/시드 반복의 체크섬이 다르면 종료코드 3.
- 1시간 등가 live 힙: `scenes=B k=5000 seeds=1 ticks=216000 memevery=21600 cap=0` (표본마다 GC.GetTotalMemory(true), 틱 시간 밖).
- 비교: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/perf_compare.ps1 base.json new.json [-Threshold 10] [-MinMs 0.05]` -> 10% 초과 악화(절대 0.05ms 이상) 표시, 체크섬 차이도 검사, 종료코드 1.
- 밤샘: `tools/perf_overnight.ps1 -Label x [-Hour] [-Compare tools/perf/baseline_main.json]` (단계마다 heavy_gate 통과).
## 설계 메모
- 측정 코드는 World.Profile 일 때만 도는 기존 Prof 훅을 재사용 -> 시뮬 코드 무수정, 체크섬 불변(detcheck GOLDEN 8변형 동일 확인).
- FindTarget / Fog 구간 합은 기존 훅으로 충분. 하위 구간(FindTarget 호출 수 · 순회 유닛 수 · Fog Decay/Stamp/Remember)용 슬롯은 PerfProbe(On=false 기본, 분기 1회)에 준비만 했고 호출 지점은 없음.
- 호출 지점 후보 (병합 뒤에 추가, 이번엔 안 넣음): WorldCombat.FindTarget 진입 `PerfProbe.Count(FindTargetCalls)` + 후보 루프 `Count(FindTargetUnitsIter)`; FogOfWar.Run 의 Decay / 유닛 Stamp 루프 / Remember 를 `Begin()/End(FogDecay|FogStamp|FogRemember, t0)`.
- KillSources 플래그화(PERF_OPT_PLAN §2 b)는 Combat3.cs 1줄이라 병합 뒤로 미룸.
## 결과
- 빌드 OK, detcheck 8변형 GOLDEN 동일(8aa6448b9690b922 262d0252c35087e2). 기준선: tools/perf/baseline_main.json (+ _hour). 수치는 아래 보고 참조.
