# T35 IsAi 리플레이·스냅샷 복원 (AF1)
- id: T35 | 역할: sim | 등급: D上 H2 (Snapshot.cs:83 · Replay.cs:22 한 줄 + WorldChecksum 한 줄; R3 S Wlight)
- 선행: 없음 (main) | 후행: AE2 AI 난이도 배수 안 A (FV 61 / Replay 25), T31c AI 전용 항복 확대, AG1
- 핫스팟: Snapshot.cs(FormatVersion) · Replay.cs(FormatVersion · 머리말) · WorldChecksum.cs · ReplaySelfTest.cs(호출 한 줄)
- 브랜치: kd/isai-restore (kd-develop/kd-isai-restore) | 상태: wip-ready | 담당: sim (Opus)
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- AF1 승인 2026-10-22 13:10: IsAi 를 리플레이 머리말·스냅샷에 포함, FV 60 / Replay 24 (ops/FV_RESERVATIONS.md).
## 범위 밖
AI 난이도 배수(AE2) · 항복 값 변경 · IsAi 를 읽는 새 규칙.
## 수용 기준
자유전 AI 리플레이 체크섬 일치 · 재생 항복 판정 동일 · 스냅샷 왕복 · 체크섬 반영 · replay 묶음 PASS · detcheck 8변형 동일.
## 결과
- 원인: Faction.IsAi 를 Surrender.cs:39 가 읽는데 리플레이 생성 경로(표준·교전·미션)는 AI 를 안 붙여 재생 시 전부 false. 미션 리플레이도 같은 결함(AttachMissionAi 는 게임 쪽만). 그 밖의 시뮬 IsAi 읽기 없음.
- 수정: 머리말 `AiFactions`(Replay 24) 를 CreateWorld 가 틱 0 전에 적용(스냅샷 시작이면 스냅샷 값). 스냅샷 본문은 이미 IsAi 저장·Audit 등록 → 체크섬에만 `if (IsAi) h.Add(0x4149)` (사람 세력은 섞지 않음 → AI 없는 detcheck 골든 불변). FV 60: 옛 저장의 체크섬이 달라져 형식으로 거부.
- 숫자: 교전 50 vs 100 전술 AI 4분(항복 문턱 시험값) — 재생 체크섬 일치 · 항복 27=27명; 머리말 비우면 27→17명 · 틱 600 어긋남(옛 결함 재현).
- 시험: replay(31 OK) · savecompat · snapshot · surr · factions · missions PASS, snaptest OK, detcheck 8변형 8aa6448b GOLDEN match(재생성 안 함).
