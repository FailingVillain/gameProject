# T04 wip/orderq 복원 · 병합 (사격 통제 · 명령 대기열 · 사선 표시)
- id: T04 | 역할: sim(복원·검증) → reviewer(R3) → merger | 등급: D上 H3 (선택 메모: R3 SM Wlight)
- 선행: T03 | 후행: T05
- 핫스팟/만질 파일: Snapshot/Replay, GameCommand, WorldCommands, Organisation, World, OrderQueue(신규)
- 브랜치: main 에서 새 브랜치 `orderq` 에 `docs/legacy/wip_patches/orderq.patch` 복원(wip/orderq 는 원본 스냅샷, 그대로 병합 금지) | 상태: wip-ready (브랜치 `wip/orderq2`, `wip/breach2` 위 스택 — 정책 2026-10-22) | 담당 에이전트: sim (Opus)
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 사격 통제 K 순환 (사격 금지→응사→자유) 추후 검토였음 (2026-10-21); 이번 묶음 승인 (HANDOFF 2026-10-22)
## 범위 밖
새 명령 추가 금지
## 수용 기준
번호 예약 54/21; reviewer R3 확인
## 측정
snaptest, 리플레이 호환
## 결과 (에이전트가 끝에 3~5줄만)
- orderq.patch 를 wip/breach2(6ad6c50) 위에 --reject 로 복원 (원본 blob 없어 3way 불가), 거부 11곳 손으로 반영: GameCommand `queue`, WorldCommands case, World StepOrderQueues, ORG_ 끝 WriteComms 뒤 Write/Read/HashOrderQueues, SelfTest orderq·firectl 묶음, Replay 표본, Snapshot Audit, Game.cs 오버레이, ENGINE_NOTES.
- 번호: Snapshot FV 54 · Replay 21 (예약대로). 대기열 미사용 판은 체크섬 불변 → detcheck 19501757040f0d62 GOLDEN match (재생성 불필요).
- 검증: 빌드 오류 0, selftest 묶음 orderq · firectl · replay · snapshot(Audit) · command PASS, snaptest OK, uifuzz 30 (875 동작, 오류 0).
- 메모(reviewer): 대기열 취소 목록(CmdUnits)에 breach 의 smokegrenade · cutwire 는 없음 — 원 설계 그대로 둠 (새 규칙 추가 안 함).
