# T38h 퍼즈가 캠페인 홈을 두드리게 (--fuzz-campaign)
- id: T38h | 역할: feature (도구) | 등급: D中 H0 (UI/테스트만, 시뮬 · FormatVersion 무변경)
- 브랜치: t38h-fuzz-campaign (NO-MERGE 스택; base = t42d-schedule-log 06ee9bd + T38g 체리픽 e5f76ec(원 f5a76d4, 충돌 없음)) | 상태: wip-ready
## 배경
T42c/d 가 발견한 틈: `--uifuzz --fuzz-menu` 가 캠페인 홈에 거의 안 들어가고(합성 자료도 없음) 15탭 · 슬롯 창 · 전과 보고가 퍼즈 밖이었다.
## 결과
- 새 인자 `--fuzz-campaign` (= --fuzz-menu 포함): KD_CAMPAIGN_DIR 을 퍼즈 임시 폴더로 바꾸고(진짜 저장 무손상) 합성 CampaignState(전과 보고 · 포로 · 진급 · 타임라인 · 일수 · 사기/피로/연대 섞인 로스터)를 쓴 뒤, 캠페인 홈 밖이면 40프레임 뒤 진입, 600프레임마다 새 자료로 재진입(전과 보고 복원). 홈이면 5프레임마다(다 가본 뒤 12) 아직 안 가본 탭(없으면 무작위) 버튼 · 화면 띠 버튼을 실제 클릭. 메뉴 클릭 빈도 1/4, 겨냥 비율 80%(캠페인 모드만). 작은 [-][+] 등록 겨냥 그대로.
- 코드: UiFuzz.cs(SeedCampaign · CampaignDrive · 보고에 방문 탭/상태), Program.cs(인자 + 프레임 훅), MainMenu.FuzzCampaign, CampaignFuzzHooks.cs(읽기 전용 관찰 구멍).
- 검증: build 0, uiicons / savecompat PASS, detcheck 8변형 GOLDEN 8aa6448b 일치. `--uifuzz 30 --fuzz-campaign` x {1024x640, 1280x720, 1920x1080}: 예외 0 · 크래시 0, 방문 탭 15/15, 화면 Home/PreOp/Debrief, 슬롯 창 열림(cm-slots-frames 수백). 시드 7 · 45초 x3 도 예외 0. `--fuzz-menu` x3 도 예외 0. 발견된 UI 크래시 없음 (수정 없음).
- 한계: 30초 안에 슬롯 저장/불러오기 확인 버튼 · 연대 [-][+] 의 모든 분기를 다 밟는다고 보장 못 함 (겨냥 클릭 확률). 미션 출격은 게임 퍼즈로 이어져 시간을 먹음.
