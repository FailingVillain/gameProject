# T22 얼굴 프로토타입 재개 (face_proto, game-tools)
- id: T22 | 역할: art(model) | 등급: D中 H0 (선택 메모: R1 S Wlight)
- 선행: 없음 | 후행: 없음 (사용자 시트 승인 뒤 적용 여부 결정)
- 만질 파일: game-tools/image/tools/face_proto.py (단독, 게임 import 없음)
- 브랜치: `gt-face-proto` (main d2c05d5 에서 신규; 원본 스냅샷 wip/gt-face-proto 6ee1c60 의 face_proto.py 를 이어받음) | 상태: wip-ready | 담당: art(Sonnet)
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 확인 필요 (HANDOFF 의 얼굴 표정 지시 원문 미확인; 프로토타입 3표정 = 무표정/외침/고통)
## 범위 밖
게임 import·스프라이트 팩 반영·다른 유닛. 사용자 승인 전 병합 금지.
## 수용 기준
128x128 흉상 위 3표정 x 2방향(s, se)이 구분되고 콘택트 시트가 docs/art_review 에 있다.
## 측정
python tools/face_proto.py <out.png> (~11초, 가벼움).
## 결과
- 눈/눈썹 3x2·2px 로 키우고 표정별 눈썹 기울기(외침=V, 고통=안쪽 올림), 입(외침=이빨+벌린 입, 고통=이 악문 입+입꼬리) 강화.
- 시트: korean-division/docs/art_review/face_proto.png
- 알려진 흠: 얼굴이 51x28px 라 눈 표정 차이가 작고 입 위주로 읽힘; 헬멧 챙에 가려 눈 위치 제한; 흉상 1종(infantry-regular)만.
