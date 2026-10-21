# Gemini 복제본 가져오기 (2026-10-22)
- 복제본 korean-division-gemini 확인 후 폴더 삭제. 브랜치 `gemini-import` (워크트리 kd-develop/kd-gemini-import), main 미병합, 빌드만 통과(selftest 미실행, 미검토).
- 가져옴(커밋 5e5ce86까지 fast-forward): settings/cmdcard/quickstep/pronefix 병합 결과 + 충돌 수정(infantry-default layout 3개, LodSprite wave_flag UsedAnimNames).
- 가져옴(WIP 커밋): 미커밋 캠페인 작업 — CampaignEconomy/Events/CodexMenu/StringsCodex, campaign_events.json, CampaignState/Carry/MissionDef/CampaignMenu/Hud/Game/MissionSelfTest 변경(캠페인 상태 변경 = 세이브/결정론 영향 가능, 병합 전 sim 검토 필요).
- 버림: docs/soak 로그 변경(테스트 산출물), docs/art_review 임시 PNG, 복제본 txt 2개(정본과 동일), GEMINI_WORK_NOTE.md, bin/obj.
- 비밀 파일 없음(ignored: .claude, bin, dist, obj, reports).
