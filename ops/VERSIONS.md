# VERSIONS - 마일스톤 (사용자 2026-10-22)

| 버전 | 내용 | 조건 |
|---|---|---|
| **0.2** | 밸런스 패치 **이전** 버전 = 기능 구현 완료 + 병합(통합) + 기준선·골든 한 번 재생성 | 기능 카드 wip-ready → 통합 리허설 → 병합(사용자 허가) → release |
| **0.3** | 밸런스 패치 **이후** 버전 = 밸런스 패스를 **한 번에** 진행 | 0.2 병합 후, 사용자 결정으로 시작. 측정 n>=24, 시드 구간 2개, 종료선 50% 기준선(kd/endline-50) |
| **0.4** | **AI 개선** | 0.3 이후. 전술 AI 3C/3D·기병 측면·AI 연막/철조망·항복 확대·AI 난이도 배수 등 |

분류(현재 카드):
- 0.2 (진행): 전투 기능(돌파·명령 대기열·통신·착검·열차·**IsAi 복원 T35 = 인프라**), 캠페인(슬롯·탄약·홈·학파), UI(T38), 지도 편집기(T33), 하천(T39), 재렌더(T11)+art 후속(T39e~j), T36·T37 수정, 통합 리허설.
- 0.3 (보류): 모든 밸런스 항목(QUEUE '밸런스 보류' 절). **AG1 AI 항복 켜기(T31c 항복 키 켜기)는 0.3 으로 이동**(사용자 결정 (45), DECISIONS BK; 0.3 계획서 Q3 에 기록, 2026-10-22 밤 3차).
- 0.3 (재보고 응답 2026-10-22 밤, 추가): T61d 자동 배정·중대 병합 UI · 병합 시 장교·장비 처리(Q-F) · 포병통 '포병 지연' 연결(31번, 통신 알파 조각 ③·포 블록 매핑과 함께) · 인사관리 UI 신설(T61e 후보).
- 0.3 (화풍 결정 2026-10-22 밤): 화풍 중간값 전환 — 비교 시트 → ART_SPEC 규칙 확정 → 마지막 일괄 재렌더에 채도·낡음 표현 적용. [확정 2026-10-22: mid 프로파일 — ART_SPEC 참조]
  - 0.3 초기: 건물·지형 mid 재렌더. 확정(2026-10-22): 건물·지형 타일·소품·데칼 전부 포함(차량·열차 제외 → 유닛 쪽 0.3 후반). 소요: 건물 1~2시간 · 지형 1시간 이내 · 소품 30분 이내 · 데칼 소규모, 합계 약 3~4시간 렌더. 엎드려쏴 4샤드·간도 복식은 지금 계속(사용자 결정).
  - 0.3 후반: 유닛 재렌더
- 0.4 (AI): T34a~e, T32b·T32c(AI 연막·철조망), AI 난이도 배수 안 A(AE2), T10b 이후 AI 항목. (T31c 항복 키 켜기는 위 0.3 으로 이동) **진행 중인 T32b·T34a 는 마무리(기본 꺼짐 키)하되, 남은 AI 카드(T32c, T34b~e)는 0.4 로 미룸.** (사용자 정정 시 변경)
- 0.4 (효과음): 인게임 효과음 재정비 (사용자 2026-10-22 밤).

## 마일스톤 전환 절차 (사용자 2026-10-22 밤 2차)
- 원칙: 현재 수준에서 버그 수정 후 0.3 으로 넘어간다. **사용자가 명령할 때만** 마일스톤을 0.3 으로 올리고 백업 절차를 실행한다. 명령 전에는 실행하지 않는다.
- 절차 (v0.2.1 백업과 동일):
  1. 기준 = merge/0.2-final 선단 커밋(명령 시점) → 주석 태그 v0.3.
  2. `git bundle`: 태그만 번들 v0.3.bundle + 전 브랜치 번들 v0.3-all-branches.bundle → C:\works\_backup\KoreanDivision · D:\KoreanDivisionBackup. 태그만 번들 v0.3.bundle 은 G:\내 드라이브\KoreanDivisionBackup 에도.
  3. 갱신: ops/VERSIONS.md · ops/STATE.md · ops/AUTO_DEFAULTS_LOG.md.
  4. 백업 폴더 삭제·이동·이름변경 금지.
- 근거: ops/STATE.md '마일스톤 v0.2.1 백업 완료' (22:30), ops/AUTO_DEFAULTS_LOG.md 2026-10-22 '사용자 명령: 0.2.1 백업 실시'.

### 0.3 기준선 범위 (사용자 결정 2026-10-22 밤, DECISIONS BO)
- 포함 (완료·리뷰·시험 통과분만): 병합 N2 전체 · bugfix-1 · bugfix-2 · bugfix-3 · T50c · g3-win-and-delay · ti5-en-draft · b1-import · branch-icons-16 · props-shape-fix · d-train-sprites · ruins-template · ruins-template-ko · t51c · t52b · rv1.
- 제외 (진행 중): kd/prone-rerender-* (엎드려쏴 4샤드) · kd/t39g-gando (간도 복식) · kd/bridge-rework (나무 다리) · kd/water-foam-fix (기슭 거품) · ui-lowfix · skill-grade-labels · T51d · T61 · T57 · TF-C 등 신규.
- 제외 (골든 변경): kd/water-fire-open (waterBlocksFire=false) → 0.3 태그 뒤 '0.3 초기'에 병합하고 골든을 그때 일괄 재생성.
- 승격 시점: 병합 N3 후 전체 selftest 통과 + 사용자 '명령'. 00:30 은 목표 시각일 뿐이며 서두르지 않는다.

- 0.3 (승격 2026-10-23 03:29): 태그 v0.3 = 2531ec16 (merge/0.2-final 선단, post-N3f-1 selftest 133 그룹 PASS + detcheck 일치, Config 0.2.1, FV Snapshot 74/Replay 27/스키마 15). 백업: v0.3.bundle(태그 이력)·v0.3-all-branches.bundle → C:\works\_backup\KoreanDivision, D:\KoreanDivisionBackup (+태그만 G:). MANIFEST C:\works\_backup\MANIFEST-v0.3.txt. 승격 스크립트 verify 단계 PowerShell 오류로 중단 → 수동 검증 완료.
