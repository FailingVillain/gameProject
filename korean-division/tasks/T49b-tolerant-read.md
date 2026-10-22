# T49b 관대한 읽기 (설정 · 사용자 작성 지휘관 JSON)
- id: T49b | 역할: feature | 등급: D中 H1 (R2 S Wlight) | 버전 0.2
- 선행: T48a | 후행: -
- 파일: UI/UserSettings.cs(Load) · Tests/TolerantReadSelfTest.cs(신규) · Tests/SaveCompatSelfTest.cs(호출 1줄)
- 브랜치 `t49bc-compat-tests` (t48a-commander-data e31406d 위, T49c 와 같은 브랜치) | 상태: wip-ready
## 확정 결정
- ops/SAVE_COMPAT_POLICY.md: 설정은 모르는 키 무시, 이상한 값은 그 키만 기본값, 파일째 못 읽으면 .bak 보존 후 기본값, 크래시 금지. 사용자 작성 파일은 못 읽으면 건너뛰고 사유 표시.
## 범위 밖
지도 편집기(T33)의 지도/미션 JSON 읽기 (이 베이스에 편집기 로더 없음 - 편집기 구현 카드에서 같은 규칙 적용). 목록 화면 UI 표시.
## 수용 기준
1) 시험 14건 (설정 6 + 지휘관 2 + 기존) PASS 2) detcheck GOLDEN 불변 3) uifuzz 30 크래시 0
## 결과
- UserSettings.Load: Data 속성을 리플렉션으로 키별 읽기 (t38a 가 Data 에 키를 더해도 자동 적용). LoadMessages(한국어) + stderr. 못 읽은 파일은 settings.json.bak (있으면 .bak.1..) 로 복사, 복사도 실패하면 Save 차단.
- 지휘관 로더(T48a)는 이미 항목 단위 거부 · 빈 풀 · 한국어 사유라 코드 변경 없음, 52건 퍼즈 시험만 추가.
- 병합 주의: t38a-settings-keys 도 UserSettings.Load 를 고친다 - 병합 시 Load 앞부분(읽기)은 이 카드 것, 클램프/적용 줄은 t38a 것을 유지.
