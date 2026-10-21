# MERGE_QUEUE - wip-ready 브랜치 순서 + 버전 번호 예약 (2026-10-22)

규칙(초안 §6): wip-ready >= 3 이면 merger 한 명이 이 순서대로 직렬 처리, 그동안 병합 잠금(`.git/kd-merge.lock`)은 병합 담당만 쥔다. 병합 직전 검증 = 빌드 · detcheck · 자기 묶음 · snaptest; 전체 selftest 는 큐 마지막(T05) 1회.

## 현재 main 값 (코드 상수에서 읽음)
- Snapshot.cs `FormatVersion = 52` (마지막: comms 통신 · 난이도)
- Replay.cs `FormatVersion = 19` (마지막: comms 대대장 앞 명령 전달)
- main 해시 `4d19119`. 주의: 위 두 값은 병합 때마다 다시 읽을 것.

## 병합 완료 표 (2026-10-22 오늘 main 에 들어간 것)
규칙: 조사·측정 작업 카드는 시작 전에 이 표를 grep 해 이미 구현됐는지 확인한다.
커밋 해시는 새 저장소에 이력이 없어 '없음(스냅샷 이전)'; 브랜치명은 확인된 것만.
| 해시 | 브랜치 | 들어간 것 |
|---|---|---|
| 없음 | assaultinf | 돌격보병 강화 (FV49) |
| 없음 | 확인 필요 | 기마 포병 |
| 없음 | 확인 필요 | 저장 호환 정책 (FV 그대로) |
| 없음 | arty3 | 포병·중화기 묶음 (FV50, Replay18) |
| 없음 | 확인 필요 | 한국군 타찬카 (문서) |
| 없음 | 확인 필요 | 신규 병종 4종 (사냥꾼·민단·엽총수·초안 마적) |
| 없음 | 확인 필요 | 튜토리얼 챕터0 미션 2개 |
| 없음 | 확인 필요 | 몽골/마적 외형 |
| 없음 | formation | 대형 완화 (FV51; choke·산개 횡대는 꺼짐) |
| 없음 | comms | 통신 알파 ① 난이도 구조+명령 전달 기반 (FV52, Replay19) |
| 없음 | 확인 필요 | trench_assault 진단 문서 |
| 없음 | 확인 필요 | S1~S3 진단 문서 |

## 병합 순서 (HANDOFF 병합 큐: 돌격보병 → 대형 완화 → 포병 → 기병 → 참호 돌파 → 사격 통제 → AI 3B; 앞쪽 4개는 이미 main)
| 순 | 브랜치 | 카드 | R | 상태 | 비고 |
|---|---|---|---|---|---|
| 1 | wip/gemini-import | T01 | R2 | wip 스냅샷(패치 보관) | 패치 복원 필요, 저장 트랜잭션 수정 선행 |
| 2 | wip/bigbldg | T02 | R2 | wip 스냅샷 | 지도 · 엄폐 · 길찾기 갱신, 철도(T07) 선행 |
| 3 | wip/breach | T03 | R3 | wip 스냅샷(패치 보관) | Snapshot/Replay 번호 재조정 |
| 4 | wip/orderq | T04 | R3 | wip 스냅샷(패치 보관) | 번호 재조정, breach 뒤 |
| 5 | wip/ai3b2 | T05 | R3 | wip 스냅샷(패치 보관) | 마지막, 전체 selftest 1회 |
| 후 | wip/rail | T07 | R2 | 패치 보관 | bigbldg 뒤 |
| 후 | wip/casualty | T06 | R3 | 브랜치 | D19 결정 뒤 |
| 후 | wip/chokefix · battlereport · doctrines · sfxnew | - | 확인 필요 | 확인 필요 | 카드 없음, 내용 확인 필요 |
| 비병합 | wip/rerender-1 · gt-brender · gt-face-proto · gt-rerender-prep | T11 | R1 | game-tools 쪽 | 렌더 예약 3칸 |

## 버전 번호 예약표 (병합 담당이 병합 때 최종 확정; 예약은 추정)
지시문의 "breach 51→" 는 구 값이다. main 이 이미 Snapshot 52 · Replay 19 이므로 다음 번호부터.
| 브랜치 | Snapshot 예약 | Replay 예약 | 변경 내용 | 확정 |
|---|---|---|---|---|
| breach | 53 | 20 | 연막 · 철조망 절단 상태 / 명령 | 미확정 |
| orderq | 54 | 21 | 명령 대기열 / 사격선 | 미확정 |
| ai3b2 | 55 | 확인 필요 | AI 블랙보드 3B (46 은 구 3B, 재확인) | 미확정 |
| casualty | 확인 필요 | 확인 필요 | 데이터 값 위주일 가능성 | 미확정 |
| rail | 56 | 확인 필요 | 철도 지형 (지도 직렬화) | 미확정 |
| bigbldg | 확인 필요 | 확인 필요 | 지도 크기 변경이면 Snapshot 영향 | 미확정 |
| gemini-import | 확인 필요 | 확인 필요 | 캠페인 저장은 별도 schemaVersion | 미확정 |
번호가 겹치면 병합 순서가 앞선 쪽이 먼저 번호를 가진다. 골든 재생성은 큐 전체에서 1회.
