# T19d 장교 학파 복원 (wip/doctrines) · 참모진 탭 읽기 전용 (T19 분할 4/4)
- id: T19d | 역할: feature | 등급: D中~上 H2 (선택 메모: R2 S<=70k Wlight)
- 선행: T19c | 후행: T12(측정 때 스택 기준)
- 핫스팟/만질 파일: assets/data/doctrines.json · Core/Doctrines.cs · Tests/DoctrinesSelfTest.cs (wip/doctrines 4파일 +193줄) · (Defs.cs 등록 불필요: 로더는 독립) · SelfTest.cs 2줄 · CampaignMenu 참모진 탭
- 브랜치: `t19d-doctrines` (**t19c-home-tabs 위 스택**; wip/doctrines 에서 4파일 가져와 복원) | 상태: wip-ready | 담당: feature(Sonnet)
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 학파 5종(독일·불란서·노서아·평양·북경) + 적 세력별 성향 표 승인 (사용자 2026-10-21, docs/OFFICER_DOCTRINES.md). 학파 = 주된 영향, 성격 = 작은 보정. 연구 연결 (사용자 승인).
- 홈은 학파를 **표시 · 연구 비용/기간 보정 근거로만** 사용. 전투 AI 적용은 별도(ROADMAP_LONG '장교 학파', 장기). AI 경향 수치(axes)는 null 유지 — 수치 발명 금지 (설계 §13-2).
- 설계 §13-3: 쌓는 순서 gemini-import → doctrines (스택으로 충족).
- 설계 정본: `docs/CAMPAIGN_HOME_DESIGN.md` v0.4 (브랜치 spec/t23-campaign-home 42fa456 에서 읽기: `git show spec/t23-campaign-home:korean-division/docs/CAMPAIGN_HOME_DESIGN.md`). 답변 정본 = ops/DECISIONS.md C2 (Q1~Q10) · F (무응답 기본값).
## 범위 밖
전투 AI 반영, 학파 axes 수치, 연구 탭 본문, 인물 신규 창작
## 수용 기준 (측정 가능)
1) doctrines.json 로드·DoctrinesSelfTest PASS 2) 참모진 탭(8, 우 화면)이 figures 를 읽기 전용으로 표시 3) 시뮬 미사용 → detcheck GOLDEN 불변 4) 충돌: SelfTest.cs 2줄만 (Defs.cs 는 로더 독립이라 등록 · 병합 없음)
## 측정
build 오류 0 · selftest(DoctrinesSelfTest 포함) · detcheck
## 결과 (에이전트가 끝에 3~5줄만 기록)
- 2026-10-22 Sonnet: wip/doctrines 4파일 복원 (doctrines.json · Doctrines.cs · DoctrinesSelfTest · SelfTest.cs 2줄 클린 적용; Defs.cs 등록 없음 = 독립 로더, 병합 충돌 없음) + 참모진 탭 읽기 전용 (UI/CampaignHomeStaff.cs, 학파 5종 목록 + 상세 + figures; axes 미표시).
- 검증: build 0, doctrines/savecompat/missions PASS, detcheck GOLDEN match 8aa6448b, uifuzz 30 크래시 0, KD_CAMPAIGN_VIEW=right:8 스크린샷 확인. 브랜치 t19d-doctrines (t19c-home-tabs 위).
- 2026-10-22 리뷰 수정: Validate 가 null 목록/이름/학파 거부(친절한 메시지), 참모진 탭 그리기 `?? empty` · 빈 학파 가드, 테스트(파일 없음 · null · 빈 schools) 추가, 인덱스 [3] 가드, CampaignHome/CampaignMenu 주석 (참모진 = 6번째 읽기 전용).
