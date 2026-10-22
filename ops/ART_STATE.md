# ART_STATE - 아트 보조 지휘자 현황 (2026-10-22 18:25)

## 한도 (사용자 지시)
- 아트 렌더 레인 최대 8 (6 시작, 빈 RAM >=6GB 유지 시 8). 비아트 heavy(빌드/selftest/stress) 최대 3, 풀 분리. 빌드 slot 1개는 항상 비움.
- 발주 전 preflight 필수. 남의 PID 종료 금지. kd-rerender 의 make_unit/rerender96/tools/tune/units/anims 는 T11 완료 전 편집 금지.

## T11 (kd-rerender e589068) 실측
- 큐 81 중 59~60 유닛 완료. rerender96 --list: 남은 1 (officer-russian-red) + heavy-field-gun-russian-white 진행 추정. deferred 10 (infantry-bandit/thug-bandit/dadao-fengtian/gando/scout, officer-bandit, cavalry-mongol/bandit, heavy-horse-artillery, heavy-tachanka)은 미착수 (T39g 와 겹침).
- 레인 6 드라이버 python: A 17012, B 16752, C 25692, D 25672, E 16196, F 34248 (모두 `rerender96.py`). 실제 make_unit 작업 중 = PID 29988, 34316 (2개).
- 대기 전용 `heavy_gate.ps1 -Render` 프로세스 4개 (PID 37632, 4312, 10176, 32716): 렌더할 유닛이 거의 안 남아 대기. 게이트 로그 "timeout: heavy=8~10 - proceed carefully" 반복 = 풀 초과 상태. 빌드/스모크(T44 bin/t44 PID 36176)를 굶긴 원인으로 추정.
- 조치: 남의 PID 는 종료하지 않음. 남은 유닛이 끝나면 드라이버가 자연 종료. 상위 지휘자가 원하면 대기 드라이버(C/D/E/F)의 정리를 결정. 추가 렌더 레인은 올리지 않음(여유 RAM 5.7GB < 6GB).
- T11 완료 전 T39e/f/g/h/j 착수 불가 (편집 금지 규칙).

## 샤드 / 에이전트
| 카드 | 샤드 | 등급 | 상태 | 산출 |
|---|---|---|---|---|
| T56a | pack_sheets.py + 명세 + infantry-regular 시범 (렌더 0) | sonnet | 완료 wip-ready kd/t56a-pack-sheets 0b6ebc3 / gt 0b... e0d3fd5 | docs/art_review/t56a_pack_pilot.png |
| T55i | 민가 42종(66키) import (T55s 미병합이라 게임 미사용) | sonnet | 완료 wip-ready t55i-house-import 7570c6f | kd-t55i/korean-division/docs/art_review/t55i_houses_imported.png |
| 공병 엎드려쏴 근본 재렌더 | pioneer 1유닛 (kd-pioneer-prone) | sonnet | 실행 중 (렌더 1레인, agent a4333a37d1ff065be) | docs/art_review/pioneer_prone_rootfix.png 예정 |

## 막힘
- T56b: T01(gemini-import) + T55s 병합 + T56a 병합 선행. T56c: T56a + t32d 병합. T56d: T56b/c + prone-wide-assets. T56e: T56d + T55 병합.
- T39e/f/g/h/j: T11 wip-ready 대기. T39i: 이미 wip-ready (시트 검수 대기).

## 다음 (T11 종료 후)
T39f(haiku, 재질 확인) / T39j(sonnet, 측정) / T39e(sonnet) / T39g(sonnet) / T39h1,h2(sonnet, 포즈 코드 겹침 주의로 h 와 j 순차) = 최대 5 레인 동시 가능. 렌더 레인은 8 이하.

## 검수 필요 시트
t56a_pack_pilot.png, t55i_houses_imported.png, (예정) pioneer_prone_rootfix.png
