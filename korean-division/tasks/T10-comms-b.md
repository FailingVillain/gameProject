# T10 통신 알파 ②~④ (대포병 · 야간 · 행군 피로)
- id: T10 | 역할: sim | 등급: D上 H3 (선택 메모: R3 SM Wheavy)
- 선행: 알파 ① 병합됨; D15 ③④ | 후행: T12
- 핫스팟/만질 파일: Comms, Snapshot, Replay, AI 배수
- 브랜치: `kd/t10-comms-b` (wip/breach2 위 스택) | 상태: wip-ready | 담당 에이전트: sim (Opus)
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 알파 ① 사단 사령부·통신 지연 (ROADMAP_LONG), ② 대포병 ③ 야간 전투(조명탄) ④ 행군 피로
- 난이도 슬라이더에 AI 정확도·탄약·증원 묶음 (승인 2026-10-22)
- D15: 쉬움 전령 사로잡힘 끔 / 보통 1.0 / 어려움 후보는 카드에서 제시, 보류
## 범위 밖
캠페인 화면
## 수용 기준
번호는 병합 시점 main 에서 재예약; 난이도 3종 시험
## 측정
통합 측정(T12)
## 어려움 후보 (제안만, 승인 전 적용 금지 — difficulty.json hard 는 1.0 유지)
A 가벼움 정확도 1.10 · 탄약 1.20 · 증원 1.00 / B 중간 1.15 · 1.30 · 1.25 / C 강함 1.25 · 1.50 · 1.50. (AI 배수 적용 지점은 아직 없음: 세력 IsAi 는 리플레이에 없어 시뮬이 읽으면 안 됨 → 적용 방식 결정 필요)
## 결과 (에이전트가 끝에 3~5줄만)
- 번호 예약: Snapshot 57 · Replay 22 (54~56 / 21 = orderq · ai3b2 · rail 예약 회피, 병합 때 재예약). MERGE_QUEUE 예약표에 지휘자가 기록 요망.
- ②③④ = `src/Core/FieldOps.cs` + `assets/data/fieldops.json` 모두 기본 끔 (세부 규칙 · 수치는 초안, 사용자 승인 대기). D15 A: easy courierCapture=false, normal/hard AI 1.0.
- 검증: 빌드 경고 0 · fieldops 16/16 · replay snapshot savecompat command comms breach weather combat3 artyai groundfire shells arty3 scouts supply PASS · detcheck 8 변형 GOLDEN 일치 (19501757040f0d62, 골든 불변) · snaptest quick FAIL 0.
- 미구현 (질문): AI 조명탄 사용 · 조명탄 버튼/화면 표시 · AI 배수 적용 지점 · 장기 행군 피로 누적 여부.
