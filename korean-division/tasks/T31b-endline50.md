# T31b-endline50 대대전 측정 시나리오 종료선 70 -> 50 + 새 기준선 (데이터 3줄 + 측정, 엔진 코드 0)
- id: T31b-endline | 역할: measure | 등급: D中 H0 | 선행: T31b 결과 (사용자 승인 AK1, 2026-10-22)
- 브랜치: kd/endline-50 (main 0ca2157 위) | 상태: wip-ready | 담당: measure(Sonnet)
## 확정 결정
- 사용자 승인 AK1 (2026-10-22): 대대전 측정 시나리오 종료선 70% -> 50% (캠페인 미션 불변), BASELINE 새로 잡기.
## 변경 (assets/missions 3파일, 각 파일 percent 2곳 + 목표 문구 1곳)
- ai_bn_meeting.json, ai_bn_assault.json, ai_kr_vs_ft_1000.json: `objectives eliminate.percent` 70->50 (봉천군 격멸 = 승), `failures losses.percent` 70->50 (한국군 손실 = 패), 목표 text "봉천군 70% 격멸" -> "50% 격멸".
- 다른 ai_* 는 해당 없음: ai_kr_vs_jp_trench 는 percent 키 없음. 건드리지 않은 70% 미션: trench_assault(eliminate 70) · armour_test · artillery_spotters · bandit_raid · c0_01 · c0_02 · c1_01 · c1_02 · forest_ambush · units_showcase · village_defense · yalu_crossing (캠페인 · 일반 미션, 범위 밖).
## 시험
- detcheck 8 변형 동일 (8aa6448b9690b922...), GOLDEN match = golden 불변 (detcheck 은 이 미션들을 안 씀, 재생성 없음).
- 전체 selftest: FAIL 4 (N12 빈자리 점검 · gapfill 예외 · 숲 탐색 켬/끔). 원본 JSON(70) 으로 되돌린 같은 빌드도 정확히 같은 4건 FAIL -> 이 변경과 무관한 기존 main 실패 (미션 선택 시험은 전부 통과). 별도 확인 필요 항목.
## 측정 (새 기준선)
build: 이 브랜치 (= main 0ca2157 + 위 JSON), `--tactics-obs bin/endline/assets/missions/<m>.json --seed 1..16 --seconds 600`, 48판, 2병렬, heavy_gate 후. 원자료 kd-endline50/_out/r (커밋 안 함). 승 = 한국군(F1) 승, 손실 = 종료 시점 병력 대비.
| 시나리오 | 승/패/시간초과 | 한국 손실 평균 (범위) | 봉천 손실 평균 (범위) | 절대 손실 한국/봉천 | K/E (합계비) | 종료 시각 평균 (범위, sd) |
|---|---|---|---|---|---|---|
| A ai_bn_meeting | 7/9/0 | 43% (26~50) | 41% (14~50) | 128 / 128 | 1.00 | 203s (97~276, sd52) |
| B ai_bn_assault | 13/3/0 | 38% (20~50) | 49% (39~52) | 115 / 153 | 0.75 | 226s (137~337, sd60) |
| C ai_kr_vs_ft_1000 | 16/0/0 | 25% (13~44) | 50% (50~51) | 246 / 500 | 0.49 | 157s (111~257, sd33) |
- 비교(T31b n=8, 종료선 50, 구 main 77d2426): A 4/4/0 K/E 0.98 193s · B 6/2/0 0.81 237s -> 이번 n=16 과 일관. 종료선 70 시절(n=8) A 3/4/1 0.87 459s · B 8/0/0 0.70 344s.
- 잡음: n=16 에서 승률 표준오차 약 +-12%p (95% 구간 약 +-24%p). A 7/16 은 "5할 근처" 이상은 못 말함; B 13/16 의 구간은 약 57~93%. 승 수 차이 1~2판은 잡음. K/E 는 시드 편차가 커서 합계비 기준(평균비 A 1.20 · B 0.76 · C 0.49). 시간 초과 0/48.
- 해석: 모든 판이 새 종료선에서 끝남 (한쪽이 50% 도달; A 는 한국 손실 상한 50%, 승패가 거의 반반). 사용자 목표 '10분 20~40% 손실' 은 평균 한국 손실 A 43 · B 38 · C 25% 로 대체로 근접 (A 가 약간 높음).
## 후속 (conductor)
- 새 기준선 사용: 이후 A/B/C tactics-obs "before" 는 아래 블록. 구 BASELINE 값(70 시절)과 직접 비교 금지.
- 70% 종료선을 가정하는 곳 (점검 결과): soak.ps1 은 값을 하드코딩하지 않음 (assets/missions 의 모든 json 을 --mission-run) -> 야간 soak 의 ai_bn_meeting/assault/ai_kr_vs_ft_1000 행이 이 병합 뒤 판 길이 · 승패 분포가 달라짐 (soak 기준선 새로). docs/TRENCH_ASSAULT_DIAG_2026-10-22.md ("70% 손실이 패배 조건") 와 docs/TACTICAL_AI_DESIGN.md · docs/legacy/BATTALION_CAMPAIGN_2026-10-19.md 는 70 시절 서술 (역사 문서, 수정 불요). tools/tobs_scale.py 는 시나리오 id 참조만. trench_assault.json 은 아직 70 (이 카드 범위 밖; 같은 규칙 적용 여부는 사용자 판단).
- T31a(재투입 제한) 재평가는 50% 선 기준으로 가능 (구 70 바닥 효과 사라짐).
## BASELINE_CURRENT 붙여넣기용 (지휘자가 붙임; 이 브랜치는 BASELINE_CURRENT.md 를 안 고침)
```
## endline50 (2026-10-22, main 0ca2157 + 측정 시나리오 3개 종료선 70->50, 새 기준선)
ai_bn_meeting · ai_bn_assault · ai_kr_vs_ft_1000 의 eliminate/losses percent 70->50 (캠페인 미션 불변). golden 불변 (detcheck 8 변형 동일 GOLDEN match). 전체 selftest FAIL 4건(N12 빈자리 점검 · gapfill 예외 · 숲 탐색 켬/끔) 은 70 원본 JSON 으로도 동일 = 기존 main 실패, 변경 무관. tactics-obs 시드 1-16 (n=16, 600 s, 새 종료선): A ai_bn_meeting 7/9/0 K/E 1.00 (한국 손실 43% · 봉천 41% · 평균 203 s) · B ai_bn_assault 13/3/0 K/E 0.75 (38% · 49% · 226 s) · C ai_kr_vs_ft_1000 16/0/0 K/E 0.49 (25% · 50% · 157 s). 시간 초과 0/48. 승 수 잡음 +-12%p (n=16). 70 시절 기준선과 직접 비교 불가; "after" 비교는 이 블록으로.
```
