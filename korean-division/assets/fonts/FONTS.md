# 글꼴 (OFL) — 종이 질감 UI용 (결정 AZ, 2026-10-22)

세 글꼴 모두 SIL Open Font License 1.1 (각 폴더의 `OFL.txt`, google/fonts 저장소 `ofl/<이름>/OFL.txt` 원문).
저작권: NHN Corporation (Naver Nanum). 원본 파일을 수정 없이 동봉(재배포 허용). 글꼴 자체를 단독 판매하는 것만 금지.
출처: https://github.com/google/fonts/tree/main/ofl (METADATA.pb `license: "OFL"` 확인). 내려받은 날: 2026-10-22.

| 역할 | 글꼴 | 파일 (정적 TTF) | 크기 | 버전(head.fontRevision) |
|---|---|---|---|---|
| 제목 (명조) | Nanum Myeongjo | nanummyeongjo/NanumMyeongjo-Regular.ttf, -Bold.ttf | 3.06 MB, 3.07 MB | 2.03 |
| 본문 (고딕) | Nanum Gothic | nanumgothic/NanumGothic-Regular.ttf, -Bold.ttf | 2.05 MB, 2.07 MB | 3.02 |
| 타자기/숫자 (고정폭) | Nanum Gothic Coding | nanumgothiccoding/NanumGothicCoding-Regular.ttf, -Bold.ttf | 2.32 MB, 2.25 MB | 2.00 |

모든 파일 < 5 MB, 합계 약 14.8 MB (assets\** 는 빌드 출력 · dist 에 그대로 복사됨).

## 용도
- 제목/장 머리/도장·날짜 머리글: Nanum Myeongjo (Bold = 큰 제목, Regular = 부제).
- 본문/버튼/목록/툴팁: Nanum Gothic (Regular 기본, Bold 강조).
- 숫자·타자기 느낌(날짜, 병력수, 표, 'KOREAN DIVISION' 로고 줄): Nanum Gothic Coding. 한글·영문 모두 고정폭(한글 = 영문 2칸).
  Courier 계열의 '타자기' 얼룩 느낌은 없음 — 종이 텍스처로 보완.

## 글자 범위 (fontTools cmap 검사)
- Nanum Myeongjo 11,882 / Nanum Gothic 11,827 / Nanum Gothic Coding 12,477 코드포인트. 셋 다 한글 완성형 11,172자 전부 포함.
- 시험 문장 `가나다라마바사 1920대한 사단 KOREAN DIVISION 0123456789 [작전 지도]`, `KOREAN DIVISION`, `코리안 디비전`, `1920 대한 사단`, `제17 대한 사단` 를
  6개 파일 x 14/18/26/40px 로 PIL 렌더 -> 빈 칸(두부) 없음, 누락 코드포인트 0. 시트: `docs/art_review/font_coverage_test.png`.
- 게임이 실제로 필요한 글자 수(2026-10-22 main, src/**/*.cs + assets 데이터): 서로 다른 문자 약 1,095개 (그중 한글 약 915자).
  미션/캠페인 추가마다 늘어남 -> 현재 `Ui.CollectCodepoints` 방식(필요한 글자만 아틀라스) 유지, 전체 11,172자 아틀라스는 쓰지 말 것.

## 알려진 주의
- 폰트 이름 'Nanum' 은 Reserved Font Name: 파일을 수정(서브셋 포함)해 재배포하면 이름을 바꿔야 함. 지금은 무수정 동봉.
- 게임 내 '서브셋' 최적화가 필요하면 새 이름으로 저장하거나, 런타임 아틀라스(LoadFontEx 코드포인트 목록)로 해결.

## 게임에서 실제로 쓰는 파일 (T38k)
- 제목 = NanumMyeongjo-Bold, 본문 = NanumGothic-Regular, 숫자 = NanumGothicCoding-Regular (`src/UI/Ui.cs` BundledFile).
- 안 쓰는 파일: NanumMyeongjo-Regular 3.06 + NanumGothic-Bold 2.07 + NanumGothicCoding-Bold 2.25 = 약 7.4 MB. 제안: 굵은 본문 · 명조 Regular 를 쓸 일이 없으면 dist 에서 빼면 글꼴 합계 14.8 -> 약 7.4 MB (OFL.txt 는 유지). 이번 카드는 파일을 지우지 않음 (사용자 결정).
- 아틀라스 로드 실측 (2247자): 시작 때 본문 16/26 = 약 36 ms, 전 단계 7개 = 약 100 ms (한도 300 ms).

