# T01 wip/gemini-import 복원 · 병합
- id: T01 | 역할: feature(복원·검증) → merger | 등급: D中 H1 (선택 메모: R2 SM Wlight)
- 선행: 없음 | 후행: T08 T09
- 핫스팟/만질 파일: 캠페인 저장(schemaVersion), UI, 패치 64MB (Program.cs · CampaignState.cs · MissionSelfTest.cs · CampaignMenu.cs 충돌)
- 브랜치: main 에서 새 브랜치 `gemini-import` 에 `docs/legacy/wip_patches/gemini-import.patch` 복원(README 참조; wip/gemini-import 는 원본 스냅샷, 그대로 병합 금지) | 상태: wip-ready | 담당 에이전트: feature(Sonnet)
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 캠페인 저장은 트랜잭션: 미션 진입 전 임시 상태, 결과 확정 시 원자적 커밋 (사용자 승인 2026-10-22, HANDOFF '역제안 A/B')
- 캠페인 저장 schemaVersion/필드별 기본값 규칙 (2026-10-22 승인)
## 범위 밖
기능 추가 없음; 설계 변경 금지
## 수용 기준
빌드·detcheck·snaptest 통과; 저장 트랜잭션 수정 포함; 패치 복원 확인
## 측정
병합 직전 검증 단축 절차(HANDOFF 2026-10-22)
## 결과 (에이전트가 끝에 3~5줄만)
- 패치 --reject 적용 후 4파일 수동 병합(Program.cs CrashLog.Context, CampaignState 중첩 클래스·SchemaVersion=2 단일화, MissionSelfTest, CampaignMenu 전면 교체 + 읽기 실패 사유 배너).
- 저장 트랜잭션: 정산(Apply→Save)은 결과 확정 한 곳에서만, 깨진·더 새 버전 파일은 새 상태로 덮어쓰지 않음(GameMission); FromJson 은 Version>=1 만 승격(0 은 Parse 가 거부).
- 검증: 빌드 오류 0, detcheck GOLDEN match, snaptest OK, KD_SELFTEST_ONLY=missions/savecompat PASS. uifuzz 미실행(merger/release 몫).
- 주의: CampaignMenu 메뉴 내 즉시 Save 는 읽기 실패 시 새 상태로 덮어쓸 수 있음(배너로 경고). 브랜치 gemini-import (main d49feac 기반).
