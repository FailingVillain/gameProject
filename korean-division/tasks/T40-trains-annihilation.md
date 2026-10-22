# T40 열차 객차를 전멸 · 섬멸 판정에 포함
- T40 | sim | 등급: D中 H1 (WorldMission.cs 판정만; Snapshot/Defs/balance 불변)
- 선행: T30e (스택 기반 t30e-demo-mission) | 후행: -
- 만질 파일: Mission/WorldMission.cs, Mission/MissionState.cs, Tests/FreightSelfTest.cs (새 섹션 8)
- 브랜치: kd/trains-in-annihilation (kd-develop/kd-trains-annih) | 상태: wip-ready | 담당: sim(Sonnet)
## 확정 결정
- 로드맵 점검 #1: 병력이 열차뿐인 세력이 전멸로 처리되던 것(T30b/T30c 리뷰)은 정확성 수정. 새 키 없음. AI 위협 계산은 0.4, 건드리지 않음.
## 범위 밖
- AI 위협 · 성향, 손실(losses) 실패 조건 퍼센트(유닛만 유지).
## 수용 기준
- 살아 있는 객차(기관차 없는 정지 열차 포함) = 생존 병력, 잔해 = 아님. 열차 없는 미션은 비트 단위 동일(detcheck GOLDEN, missions).
- FV 불변(작업 버퍼만).
## 결과
- 원인: UpdateMission 의 m.Alive 가 Units 만 셈 → 열차만 남은 세력 = 전멸 취급. 수정: AliveCars/TotalCars 작업 버퍼 추가, wiped(패배)와 eliminate(faction) 판정에 살아 있는 객차 포함(잔해 제외, 기관차 없는 정지 열차 포함). losses 퍼센트는 유닛만 유지. AI 불변. FV 불변.
- 시험: FreightSelfTest 섹션 8 (10건: 객차만 생존=전멸 아님, 잔해만=전멸, 열차 없는 미션 불변) PASS; freight · traindemo · missions PASS; detcheck GOLDEN match.
