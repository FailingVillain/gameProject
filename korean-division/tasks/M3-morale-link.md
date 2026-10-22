# M3 캠페인 사기 평균 -> 미션 시작 세력 사기 연결
- M3 | feature | 등급: D 中 · H 2 (GameMission 1줄 · CampaignEconomy · Tests)
- 선행: M1(kd/m1-morale-suppress d75a2bc) + M2(kd/m2-morale-camp 1ce9aff) | 브랜치/작업 폴더: kd/m3-morale-link, kd-develop/kd-m3-morale-link | 상태: wip-ready | 담당: feature (Sonnet 5.5)
## 확정 결정 (ops/DECISIONS BH, 사용자 2026-10-22)
- 캠페인 전체 사기 = 중대 사기의 평균, 아군 사기는 제압 하락 속도와 연동(M1), 탈영은 전투 후만(M2). 적 사기 시스템 없음(BH1).
## 결과
- CampaignEconomy.AverageCompanyMorale(로스터 중대 사기 평균, 정수 (합*2+n)/(2n) 반올림 올림, 빈 로스터 50) · MissionStartMorale(state, missionId) = 출격 기록(Pending.MissionId) 일치할 때만 평균, 아니면 null.
- GameMission.CreateMissionGameWorld 에서 null 아닐 때만 World.SetFactionMorale(human, 값) (틱 0 전). 단독 · 연습 · 자유전은 호출 안 함 = 50. 적 세력 미설정. M2 의 TODO 주석 제거.
- 시험 MoraleSelfTest.TestCampaignLink: 평균 · 미션 시작 값 · 통합(캠페인 0 회복 2397틱 > 기본 1722 > 100 1497) · 저장 왕복 · 리플레이 머리말 [0,50] 일치. morale/savecompat/replay/snapshot PASS, snaptest QUICK OK.
- detcheck 672804207fc4e486 3c42aea49324248e = kd/m1 단독과 동일 (골든 파일은 m1 라인에서 원래 미갱신 MISMATCH). FV 67 · Replay 25 는 M1 임시값 그대로 - 병합 때 fv_renumber.
- 병합 메모: m1 병합 때 CrashLog 이름 충돌(KoreanDivision.CrashLog vs UI.CrashLog)을 Program.cs 한정자 + CrashLogSelfTest alias 로 임시 해소; T48b1(FV 53)은 67 줄 설명에 메모. morale.troops 를 중대 평균 파생값으로 바꾸는 M2 후속(미커밋, kd-m2-morale-camp 작업 트리)과는 독립 - 로스터에서 직접 계산.
