# T55m 민가 import · 테마 키 · 지도 데이터
- id: T55m | 역할: art/docs | 등급: D下 H0
- 선행: T55s 필드 정의 | 후행: T55a T55b import
- 만질 파일: tools/import_kd1920_buildings.py(글롭/--only) assets/themes/kd1920/theme.json(키 규칙) assets/missions|maps 샘플 1~2개의 houseStyle
- 브랜치: kd-develop/kd-t55m | 상태: queued
## 범위 밖
렌더 · 선택 로직 코드
## 수용 기준
- 다른 유닛 폴더를 지우지 않음 (--only). import_kd1920.py 뒤 재실행 규칙 유지, 멱등.
- theme.json 키 +66 이 ops/LIMITS.md 한도 안인지 확인.
## 결과
