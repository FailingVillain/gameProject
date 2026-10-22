# T11b 엎드려쏘기 넓은 칸 에셋 가져오기
- id: T11b | 역할: art | 등급: D中 H0 | 선행: gt-prone-fix 2694c89 (렌더 완료, 미가져옴) | 후행: 파이오니어 62° 우회 재작업(별 카드)
- 브랜치: kd/prone-wide-assets (kd-develop/kd-prone-wide-assets) | 상태: wip-ready | 담당 에이전트: art(sonnet)
## 확정 결정
- 보병 칸 넓힘 승인 (자동 기본값, 2026-10-22 로그): 176x160 기본, 긴 소총 · 저격 · 기수 192x160.
## 범위
- tools/import_kd1920.py --src gt-prone-fix/.../korean-division-image-out --only (25 유닛 폴더; darkgrey/kepi 는 게임에 없는 폴더라 제외). theme.json 변경 없음 (의미상 동일, 레이아웃이 칸 크기를 가짐). C# 변경 없음 (LodAtlas 가 레이아웃 frame 을 읽음, 기관총병 선례).
## 결과
- 시트: docs/art_review/prone_wide_ingame.png (E/W 게임 안 전후). detcheck 8변형 골든 일치 (8aa6448b). units/uiicons PASS; lodaudit 는 main 과 같은 wave_flag 실패 2건 (기존).
- LOD0 상주 변화 없음 (잘라내기 뒤 동일: 8종 69 MB), 원본 시트 크기만 +30~40% (일시 빌드), 로드 시간 동일 (~5.4~6.5 s).
- 가장자리 닿음: 엎드림 셀 0. 기수 TB 9칸 (깃대, 엎드림 아님; 기존).
