# USER_DECISIONS_QUEUE (사용자 결정 대기; 한 줄 = 질문 · 추천안 · 영향. 2026-10-23 03:3x 기준; 수면 모드에서 한 줄씩 누적)
1. TI2·TI3 질문 7개(spec/ti2-plan 의 docs/specs/TI2_SPLIT_PLAN.md §6, DECISIONS_PENDING 에 7줄) · 추천: 호출부 캐시 3곳 수정 허용, N3e 문서 병합 뒤 발주 · UI 문자열 구조 변경.
2. T57f FU-5 만료 틱(OverrideSeconds): 명령에 기록 vs 머리말 저장 · 추천: 머리말 저장(FV 불변 가능) · 리플레이 안정성.
3. T61g-3 전멸 중대 부상자 소멸 규칙(3회 정산 뒤 소멸 유지/해체 전까지 보존) · 추천: 해체 전까지 보존 · 캠페인 인력.
4. T61g-4 신편 장교 선택 UI·전투 주입이 T61e(인사관리 UI)와 중복인지 · 추천: T61e 로 통합.
5. CKf-1 연습 출격 자동 저장 칸 접두 분리 · 추천: 분리 · 저장 칸 덮어쓰기 방지.
6. CKf-2 옛 전역 autosave_1..3·autosave_battle 처리(삭제/숨김) · 추천: 숨김.
7. 인사관리 UI 설계 질문 12개(spec/personnel-ui, docs/decisions_pending.d/kd__t61e-personnel-ui.md) · 0.3 에서 해제하기로 함.
8. 연대장 33명: 호감 선호·고유 특성·계급·약력 작성(사용자) → commanders.json 입력 카드(시험 Defs.Count 갱신). 12명은 테스트 동안 즉시 채용 가능 유지(결정됨).
9. 번역 열린 항목(korean-division/docs/I18N_STATUS.md): 영어 신규 키 판단 갈림, 성향 8종 영어 표기, 귀화자·외국 장군 표기, 진강산/봉황성 표기, "소 나루 마을" 오타 여부, 용어 규칙 완화 여부.
10. 태극기: 법령 원문(law.go.kr) 직접 확인 — 깃면 너비=세로 해석, 저LOD 가독성(법령 비율 유지 추천).
11. 보류 브랜치: wip/chokefix(D14), wip/casualty2 폐기 재확인, integration/rehearsal 병합 금지 유지, wip/gt-brender(render_blender.py 가져올지).
12. 0.2.1 zip(C:/works/_dist_out/KoreanDivision-0.2.1-2531ec1622.zip) 플레이 후 어색한 곳 메모 → 0.3 첫 카드.
13. 사용자 직접 조치: 하네스 환경변수 CLAUDE_CODE_MAX_CONCURRENT_SUBAGENTS 를 9 로 변경(현재 30); 삭제 대기 정리(tools/perf-bench 워크트리·브랜치, kd-rerender-5 워크트리 — 권한 필요).
14. 설계 보류 유지: UNDO-b/c(전령 번복), TPOW-c(현장 확보), CK3(이어하기), 위임 UI 개선(0.4).
