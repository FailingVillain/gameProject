# FOLLOWUPS - 소후속 목록 (지휘자 메모)
- [TC-C kd/tcc-inject 3b391cb · Gemini flash 2차 검토 2026-10-22, 지휘자가 코드 검증 전 '단서'] (1) CmdLock 만료 엔트리가 딕셔너리에 남아 체크섬에 계속 포함(EventCommand.cs:17-18, WorldMission.cs:699) — 결정적이라 불일치는 아니지만 만료 청소 또는 유효 잠금만 해시 권장. (2) 잠금 틱 = MathF.Ceiling(s / Config.SimDt) 부동소수 나눗셈(EventCommand.cs:33) — 정수 틱 변환 권장. (3) CmdLockedCompany(1명이라도 잠기면 중대 전체) vs Resolve(유닛별 필터) 비대칭(EventCommand.cs:20-25, WorldCommands.cs:126-133) — 부분 잠금 중대 정의 필요. → 병합 전 소카드로 확인·수정.
- [r2 후속] GameMapTerrain.FiringLineBlock(표시 전용)이 waterOpen 을 안 넘김(waterBlocksFire=false 일 때만 차이).
- [S7 spec 미발주] 설계서: 지도 편집기 후속·미션 공유(spec/mapeditor-share) — 동시 에이전트 20 한도로 보류, 슬롯 나면 발주.
- [PR4 결과] 소품 렌더 최적화 후보 D(Collect 그리드)·E(먼 줌 LOD)·F(LosMul seenBuf 해시셋)는 렌더 전용/저위험 후속 — 필요해질 때 카드화. 정리 대상(삭제는 사용자 몫): 워크트리 kd-develop/kd-pr4-measure, 폴더 kd-develop/pr4-out.
- [Gemini 지적 단서] M1 리플레이 머리말 Morale 배열이 Faction.Id 순서와 일치하는지(Replay.cs:71 .OrderBy(f=>f.Id) 명시 권고) — 병합 전 확인.
- [river 묶음] kd/fix-river-props 적용 뒤에도 river selftest FAIL 5건(골든 불일치·임시 분대·체크섬) — 기존 문제, 최종 합치기 PRE-EXISTING FAIL(squads)과 같은 계열 가능성.
# FOLLOWUPS - 코드 변경이 필요해 보류한 후속 (한 줄 메모)

- src/SelfTest.cs 등록 줄 충돌(main 밖 70커밋 핫스팟, 병합 로그 11회 union; 감독관 C-3/C-7): 시험 파일이 `[SelfTestGroup("name")]` 특성 또는 정적 등록 메서드로 스스로 등록하고 SelfTest 가 리플렉션으로 수집하게 바꿔 등록 줄 편집을 없앤다(중, sonnet, 시험 인프라 전용·골든 무관; 묶음 이름·실행 순서 보존 시험 필요). 이번 도구 카드(tools/pipe-parallel)에서는 코드 금지라 미구현.
