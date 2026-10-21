# T19d 장교 학파 복원 (wip/doctrines) · 참모진 탭 읽기 전용 (T19 분할 4/4)
- id: T19d | 역할: feature | 등급: D中~上 H2 (선택 메모: R2 S<=70k Wlight)
- 선행: T19c | 후행: T12(측정 때 스택 기준)
- 핫스팟/만질 파일: assets/data/doctrines.json · Core/Doctrines.cs · Tests/DoctrinesSelfTest.cs (wip/doctrines 4파일 +193줄) · Defs.cs 로더 등록 · SelfTest.cs 2줄 · CampaignMenu 참모진 탭
- 브랜치: `t19d-doctrines` (**t19c-home-tabs 위 스택**; wip/doctrines 에서 4파일 가져와 복원) | 상태: queued | 담당: feature(Sonnet)
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 학파 5종(독일·불란서·노서아·평양·북경) + 적 세력별 성향 표 승인 (사용자 2026-10-21, docs/OFFICER_DOCTRINES.md). 학파 = 주된 영향, 성격 = 작은 보정. 연구 연결 (사용자 승인).
- 홈은 학파를 **표시 · 연구 비용/기간 보정 근거로만** 사용. 전투 AI 적용은 별도(ROADMAP_LONG '장교 학파', 장기). AI 경향 수치(axes)는 null 유지 — 수치 발명 금지 (설계 §13-2).
- 설계 §13-3: 쌓는 순서 gemini-import → doctrines (스택으로 충족).
- 설계 정본: `docs/CAMPAIGN_HOME_DESIGN.md` v0.4 (브랜치 spec/t23-campaign-home 42fa456 에서 읽기: `git show spec/t23-campaign-home:korean-division/docs/CAMPAIGN_HOME_DESIGN.md`). 답변 정본 = ops/DECISIONS.md C2 (Q1~Q10) · F (무응답 기본값).
## 범위 밖
전투 AI 반영, 학파 axes 수치, 연구 탭 본문, 인물 신규 창작
## 수용 기준 (측정 가능)
1) doctrines.json 로드·DoctrinesSelfTest PASS 2) 참모진 탭(8, 우 화면)이 figures 를 읽기 전용으로 표시 3) 시뮬 미사용 → detcheck GOLDEN 불변 4) 충돌: Defs.cs · SelfTest.cs 수동 병합 기록
## 측정
build 오류 0 · selftest(DoctrinesSelfTest 포함) · detcheck
## 결과 (에이전트가 끝에 3~5줄만 기록)
