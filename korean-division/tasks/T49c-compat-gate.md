# T49c 저장 호환 출시 게이트 (캠페인 v1..현재 고정 샘플)
- id: T49c | 역할: feature | 등급: D下 H1 (R2 S Wlight) | 버전 0.2
- 선행: T49a(병렬, 독립 파일) | 후행: -
- 파일: Tests/CompatGateSelfTest.cs(신규) · SelfTest.cs(그룹 compatgate + 전체 실행 줄) · tests/fixtures/campaign/campaign_v1..v8.json · tests/fixtures/gen_campaign_fixtures.py
- 브랜치 `t49bc-compat-tests` (T49b 와 같음) | 상태: wip-ready
## 확정 결정
- ops/SAVE_COMPAT_POLICY.md: 캠페인은 항상 이행, 더 새 버전은 거부. 샘플은 한 번 만들면 고치지 않는다.
## 체크리스트 (스키마를 올리는 모든 카드)
campaign_v{새 번호}.json 을 추가: 시험 실행 `KD_SELFTEST_ONLY=compatgate KD_COMPAT_DUMP=<폴더>` 로 현재 샘플을 뽑고, gen_campaign_fixtures.py 의 COMMITS 에 (새 번호: 커밋) 를 더해 재생성 (기존 파일은 git diff 로 불변 확인). 없으면 compatgate 가 실패한다.
## 수용 기준
v1..v8 전부 열림(이행 · 기본값 채움 · Load/저장/재읽기 왕복) + 더 새 버전(v9, v13, v999) 거부 메시지 + 파일 보존
## 결과
- 샘플 = 현재 채워진 상태에서 해당 스키마 도입 커밋(git 이력)에 없던 속성 키를 제거해 생성 (v1 1KB .. v8 4KB). compatgate 23건 PASS.
- 메시지 문구는 현행 SnapMsg 로만 검사 (T49a 가 추가 줄을 붙여도 포함 검사라 깨지지 않음).
