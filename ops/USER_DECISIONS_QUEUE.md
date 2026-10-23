# USER_DECISIONS_QUEUE (사용자 결정 대기; 한 줄 = 질문 · 추천안 · 영향. 2026-10-23 03:3x 기준; 수면 모드에서 한 줄씩 누적)
(03:4x 사용자 답으로 옛 1~7번 해결: DECISIONS.md 참조. 아래는 옛 번호 유지 8~14)
8. [2026-10-23 사용자: 0.4 로 올려 보류] 연대장 33명: 호감 선호·고유 특성·계급·약력 작성(사용자) → commanders.json 입력 카드(시험 Defs.Count 갱신). 12명은 테스트 동안 즉시 채용 가능 유지(결정됨).
9. 번역 열린 항목(korean-division/docs/I18N_STATUS.md): 영어 신규 키 판단 갈림, 성향 8종 영어 표기, 귀화자·외국 장군 표기, 진강산/봉황성 표기, "소 나루 마을" 오타 여부, 용어 규칙 완화 여부.
10. 태극기: 법령 원문(law.go.kr) 직접 확인 — 깃면 너비=세로 해석, 저LOD 가독성(법령 비율 유지 추천).
11. 보류 브랜치: wip/chokefix(D14), wip/casualty2 폐기 재확인, integration/rehearsal 병합 금지 유지, wip/gt-brender(render_blender.py 가져올지).
12. 0.2.1 zip(C:/works/_dist_out/KoreanDivision-0.2.1-2531ec1622.zip) 플레이 후 어색한 곳 메모 → 0.3 첫 카드.
13. 사용자 직접 조치: 하네스 환경변수 CLAUDE_CODE_MAX_CONCURRENT_SUBAGENTS 를 9 로 변경(현재 30); 삭제 대기 정리(tools/perf-bench 워크트리·브랜치, kd-rerender-5 워크트리 — 권한 필요).
14. 설계 보류 유지: UNDO-b/c(전령 번복), TPOW-c(현장 확보), CK3(이어하기), 위임 UI 개선(0.4).
16. NEW-2 LOD0 그림 누락: 0.2-final(33ee9be3)에서 재현 안 됨. lodaudit(290종 문제 0건), 레이아웃 540개 시트·칸 범위 전수 확인, --battle 최대 줌(4.0) 샷, uifuzz 40 + KD_LODMISS=1 모두 누락 0. 코드 변경 없음. 남은 후보: (a) 0.2.1 zip 에서만 보였다면 어떤 유닛·줌·상황인지 알려 주세요, (b) 30초 안 쓴 LOD0 내림 뒤 재확대 때 몇 프레임 낮은 LOD 로 보이는 것(설계된 동작, 이를 '누락'으로 느꼈을 수 있음 — 필요하면 내림 시간 KD_LOD_EVICT_SEC 상향 제안), (c) 알려진 모션 누락 6건(bandit_thug·infantry_dadao_fengtian 사격, heavy_horse_artillery·tachanka 거치/철수) 은 새 그림 필요. (브랜치 kd/new2-lod0, 변경 없음)
