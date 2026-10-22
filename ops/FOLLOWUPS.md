# FOLLOWUPS - 코드 변경이 필요해 보류한 후속 (한 줄 메모)

- src/SelfTest.cs 등록 줄 충돌(main 밖 70커밋 핫스팟, 병합 로그 11회 union; 감독관 C-3/C-7): 시험 파일이 `[SelfTestGroup("name")]` 특성 또는 정적 등록 메서드로 스스로 등록하고 SelfTest 가 리플렉션으로 수집하게 바꿔 등록 줄 편집을 없앤다(중, sonnet, 시험 인프라 전용·골든 무관; 묶음 이름·실행 순서 보존 시험 필요). 이번 도구 카드(tools/pipe-parallel)에서는 코드 금지라 미구현.
