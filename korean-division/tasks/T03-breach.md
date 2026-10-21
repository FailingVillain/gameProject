# T03 참호 돌파 (wip-resolved/breach 검증 → 병합)
- id: T03 | 역할: sim(검증) → reviewer(R3) → merger | 등급: D上 H3 (선택 메모: R3 SM Wheavy)
- 선행: 없음 (D17 무응답 기본값 = 승인) | 후행: T04 T16
- 핫스팟/만질 파일: Snapshot/Replay 번호, Defs, balance, SelfTest
- 브랜치: `wip-resolved/breach` (7faccf0 = breach 패치를 main 8d22ad5 위에 해결, FV53/Replay20, 골든 재생성) 에서 이어감. wip/breach 는 원본 스냅샷(패치 보관), 사용 안 함 | 상태: queued | 담당 에이전트: -
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 연막·철조망·경박격포·연막 수류탄 묶음 (ROADMAP_SHORT; 원문 확인 필요)
- D17 경박격포 사거리 36 등 초안: 무응답 시 승인(2026-10-22)
- D18 AI 의 연막/철조망 사용: 스위치 꺼짐 유지
## 범위 밖
AI 연막·철조망 사용 구현(D18); 공병 선로 파괴
## 수용 기준
R3: 병합 전 reviewer 확인; 번호는 MERGE_QUEUE 예약(53/20)
## 측정
main 을 한 번 병합해 빌드 · detcheck · breach 묶음 · snaptest; 전체 selftest 는 release(T13)
## 결과 (에이전트가 끝에 3~5줄만)
-
