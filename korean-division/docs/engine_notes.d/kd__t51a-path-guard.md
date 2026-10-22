## T51a 경로 안전 + JSON 한도 (src/Core/UserData/)
- PathGuard: id `^[A-Za-z0-9_]{1,64}\z` + 예약 이름 거부 + 합친 전체 경로가 폴더 바로 아래인지 확인. `MissionDef.PathOf`/`MapFile.PathOf` 가 사용; 동봉 29개 id 의 결과는 예전과 문자 동일. 정규식은 `$` 가 아니라 `\z` (`$` 는 끝 개행 앞에도 맞음 — "x\n" 통과 버그를 시험이 잡음).
- JsonLimits.Check(text) → (Ok, Code, Reason): 4MB · 깊이 32 · 문자열 4096 · 배열 20000 · NaN/Infinity/범위초과 · 같은 객체 중복 키. 예외 없음. 시험 KD_SELFTEST_ONLY=pathguard.
