# MERGE_QUEUE - wip-ready 브랜치 순서 + 버전 번호 예약 (갱신 2026-10-22)

규칙: merger 는 **상시** — `wip-ready` 카드가 1개라도 있으면 지휘자가 merger 를 띄우고 이 순서대로 직렬 처리. 잠금(`.git/kd-merge.lock`)은 merger 만(만료 30분, docs/procedures/merge-procedure.md). 병합 직전 검증 = 빌드 · detcheck · 자기 묶음 · snaptest; 전체 selftest 는 release(T13) 1회.
쓰는 사람: merger(상태 · 완료 표 · 번호 확정) · 지휘자(wip-ready 행 추가) · release(큐 닫음 한 줄).

## 현재 main 값 (코드 상수; 병합 때마다 다시 읽을 것)
- Snapshot.cs `FormatVersion = 52` · Replay.cs `FormatVersion = 19` (둘 다 comms ①). main 해시: `git -C C:/gameProject log -1 --format=%h main`.

## 병합 완료 표 (main 에 들어간 것)
규칙: 조사·측정·spec 카드는 시작 전에 이 표 + `docs/legacy/GIT_HISTORY_DIGEST.md` 를 grep 해 이미 구현됐는지 확인. '스냅샷' = 첫 커밋 a1a35e6 에 포함.
| 해시 | 브랜치 | 들어간 것 |
|---|---|---|
| 스냅샷 | assaultinf | 돌격보병 강화 (FV49) |
| 스냅샷 | horseart | 기마 포병 (타찬카 마차 + 보병포) |
| 스냅샷 | savecompat | 저장 형식 호환 정책 |
| 스냅샷 | arty3 | 포병·중화기 묶음 (FV50, Replay18) |
| 스냅샷 | tachanka | 한국군 타찬카 확인 + 시트 |
| 스냅샷 | newunits | 신규 병종 4종 (사냥꾼·마적 엽총수·초안 마적·민단) |
| 스냅샷 | tutorial | 튜토리얼 챕터0 0-1 · 0-2 |
| 스냅샷 | lookfix | 몽골/마적 외형 |
| 스냅샷 | formation | 대형 완화 (FV51; choke·산개 횡대 꺼짐) |
| 스냅샷 | comms | 통신 알파 ① (FV52, Replay19) |
| 스냅샷 | trenchdiag | trench_assault · S1~S3 진단 문서 |
| 8d22ad5 | wip/bigbldg | 건물 크기 확대 (HouseBlocks 렌더) |
| ba696b7 | gate-lanes | heavy_gate 3레인 (기본 비활성) |
| 53712e9 · 4d19119 | - | 도구 정리 · 결정 장부 D01~D22 |
| b5918a8 · a4c79c7 | docs-ops · docs-roles | 문서 재정비 (ops/tasks, 헌장/역할/절차) |
| 562e578 | docs-fable | PIPELINE v1 · STATE/QUEUE 현행화 · T13 |
| bfe5641 | fix/n12-forest | 낡은 시험 4건 수정 (N12/Forest/GapFill 시험 3파일만; 사용자 승인 예외 2026-10-22) |
| a8552f9 | tools/fv-renumber | fv_renumber.ps1 (merge/0.2 A1) · FV 52/19 · detcheck 8aa6448b9690b922 |
| 9b2cbff | tools/monitor-launch | run_game.ps1 (A2) · AGENT_RULES 절 합집합 · FV 52/19 |
| 5d8149f | kd/crash-log | T41 충돌 로그 (A3) · FV 52/19 · detcheck 8aa6448b9690b922 · crashlog PASS · uifuzz 30 0건 |
| 32180a0 | kd/endline-50 | 종료선 70→50 미션 json (A4) · FV 52/19 · detcheck 8aa6448b9690b922 · missions PASS |
| a0611ad | sfxnew | T21 효과음 (A5) · FV 52/19 · detcheck 8aa6448b9690b922 · sounds.json 유효 |
| 2231459 · abbe77f | gt-face-proto · t30d-train-art | T22 얼굴 시안 · T30d 열차 그림 (A6, src 변경 없음) · FV 52/19 · detcheck 8aa6448b9690b922 |
| ffc4129 | wip/breach2 | T03 돌파 (B1) · FV 53/20 · detcheck 19501757040f0d62 · breach/snapshot/replay/crossing PASS · snaptest OK · uiicons FAIL 1건(정상, 카드 칸) |
| d1947b0 | wip/orderq2 | T04 명령 대기열 · 사격선 (B3) · FV 54/21 · detcheck 19501757040f0d62 · orderq/firectl/replay/command/crossing PASS · snaptest OK · uiicons FAIL 1건(정상) |
| 7d5c124 | ai3b2 | T05 AI 블랙보드 3B (B4) · FV 55/21 · detcheck 19501757040f0d62 · ai3b/snapshot/crossing PASS · snaptest OK · uiicons FAIL 1건(정상) |
| 89ca250 | kd/t24-s1s3 | T24 D08 ①②③ (B5) · FV 55/21 · detcheck ccfbd1ddab1c1dfe · cavcmd/command/drill/officer PASS · snaptest OK · crossing FAIL 2건(B5b 전 정상) · uiicons FAIL 1건(정상) |
| 21085f4 | kd/t24-crossing-fix | 도하 단계 observer keep=0 (B5b) · FV 55/21 · detcheck ccfbd1ddab1c1dfe 불변 · crossing/tactical/officer/command PASS · uiicons FAIL 1건(정상) |

**큐 닫음 (release, 2026-10-22)**: fix/n12-forest 큐 — 전체 selftest 2008 OK/FAIL 0 · detcheck 8변형 동일 · snaptest OK · dist 발행. (T03~T05 큐의 T13 은 별도, 아직 queued.)

## 병합 순서 (wip-ready 가 된 것만 처리; 예정 순서)
| 순 | 브랜치 | 카드 | R | 상태 | 비고 |
|---|---|---|---|---|---|
| 1 | wip-resolved/breach | T03 | R3 | 복원됨, 검증 대기 | FV53/R20 적용본, reviewer 뒤 wip-ready |
| 2 | gemini-import (복원) | T01 | R2 | 패치 보관 | 저장 트랜잭션 선행 |
| 3 | orderq (복원) | T04 | R3 | 패치 보관 | breach 뒤 |
| 4 | ai3b2 (복원) | T05 | R3 | 패치 보관 | 마지막; 전체 selftest 는 release |
| 후 | rail (복원) | T07 | R2 | 패치 보관 | 선행 없음 |
| 후 | wip/casualty | T06 | R3 | 브랜치 | D19 뒤 |
| 후 | chokefix · battlereport · doctrines · sfxnew | -/T17/T19/- | - | 브랜치·패치 | 카드 발주 때 복원; chokefix 는 D14 |
| 비병합 | rerender-1 · gt-brender · gt-rerender-prep · gt-face-proto | T11 | R1 | game-tools | 렌더 3칸, 시트 검수 뒤 |

## 버전 번호 예약표 (merger 가 병합 때 확정; 겹치면 병합 순서가 앞선 쪽이 먼저. 골든 재생성은 큐 전체 1회)
| 브랜치 | Snapshot 예약 | Replay 예약 | 변경 내용 | 확정 |
|---|---|---|---|---|
| breach | 53 | 20 | 연막 · 철조망 절단 (wip-resolved/breach 에 적용됨) | 미확정 |
| orderq | 54 | 21 | 명령 대기열 / 사격선 | 미확정 |
| ai3b2 | 55 | - | AI 블랙보드 3B (46 은 구 3B, 재확인) | 미확정 |
| rail | 56 | - | 철도 지형 (지도 직렬화) | 미확정 |
| casualty · gemini-import | 확인 필요 | 확인 필요 | 값 위주 / 캠페인 schemaVersion 별도 | 미확정 |
