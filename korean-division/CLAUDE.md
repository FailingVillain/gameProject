# Korean Division - 헌장 (L0, <=2KB)
C# .NET 8 + Raylib-cs 결정적 전술 캠페인 게임. 저장소 `C:/gameProject/korean-division`.

## 절대 규칙
1. **결정성**: 상태 변경은 명령(`WorldCommands.cs`)으로만. DetMath + 해시 난수, 벽시계 / 렌더 상태 / 딕셔너리 열거 순서 금지. 새 상태 = 체크섬 + `SnapshotSelfTest.Audit` + 저장 + `Snapshot.FormatVersion` 올림.
2. **push 는 사용자가 명령할 때만.** 로컬 커밋은 자유.
3. **커밋**: `git -c user.name=seung -c user.email=seungwoo3656@gmail.com commit`, 파일 이름으로 add (`commit -a` 금지), 트레일러 `Co-Authored-By: Claude <실제 모델> <noreply@anthropic.com>`.
4. **권한 프롬프트가 날 명령 금지** (삭제, 이미지 이름 kill, 저장소 밖 쓰기, push). 우회하지 말고 `docs/DECISIONS_PENDING.md` 끝에 한 줄(= ops/DECISIONS 후보) 적고 속행. 상세 `docs/procedures/permissions.md`.
5. **보고**: 카드가 정한 줄 수 안에서 결론 + 숫자 + 필요한 결정만. 긴 로그는 grep / Gemini flash.
6. **한국어**로 보고. 요청 밖 기능은 만들지 말고 제안만. 컨텍스트 ~200k 넘으면 정리 후 정지.
7. **게임 창 실행**(시각 확인 · 스크린샷 · uifuzz)은 `tools/agent/run_game.ps1` 로: 보조 모니터, 없으면 주 모니터.

## 다음에 읽을 곳
- 역할 카드 `C:/Users/seung/.claude/agents/<role>.md` (sim feature art docs measure merger reviewer release scout spec) + 작업 카드.
- 공통 규칙 `tools/agent/AGENT_RULES.md` (<=3KB), 절차 `docs/procedures/` (merge-procedure, gate, permissions, measurement).
- 빌드 · 플래그 · 검증 · 이름 규칙 · 외부 도구 · 최신 selftest: `docs/PROJECT_NOTES.md` (Grep). 한도: `ops/LIMITS.md`. 결정: `ops/DECISIONS.md`. 작업 흐름 · 문서 소유권: `ops/PIPELINE.md`.
- 심볼 위치: `PROJECT_MAP.md` Grep. 큰 문서(ENGINE_NOTES, ROADMAP_LONG, legacy/)는 통독 금지, 절만 Grep.
