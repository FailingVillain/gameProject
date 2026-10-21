# 작업 인계 (HANDOFF)

> 사용량 한계 · 세션 종료 대비. **새 세션은 이 파일부터 읽는다** (그다음 필요할 때만 `docs/ROADMAP_LONG.md` 를 Grep).
> 사용자가 "상황 저장" (또는 `/save-state`) 하면 아래 절차대로 이 파일을 갱신한다. 재개는 "이어서" (또는 `/resume`).
> 끝난 항목 = docs/legacy/HANDOFF_ARCHIVE.md (Grep 만).

## 저장 절차 (Claude 용)
1. 진행 중인 에이전트마다 SendMessage: "지금 단계에서 멈추고, 빌드되는 상태로 WIP 커밋, 남은 일 · 다음 단계를 한국어로 짧게 보고".
2. 보고를 받으면 (또는 기다릴 수 없으면 git log · git status 로 확인) 아래 "현재 상태" 를 갱신: 저장소 · 브랜치 · 마지막 커밋, 에이전트별 남은 대기열 (에이전트에 메시지로만 준 지시도 전부 옮겨 적기 — 세션이 끝나면 에이전트 대화는 사라진다), 사용자에게 받을 결정.
3. korean-division 에 로컬 커밋 (push 금지). game-tools 도 남은 변경이 있으면 WIP 커밋.
4. 사용자에게 "저장 완료 + 재개 방법" 한 줄 보고.

## 재개 절차
1. 이 파일 → "현재 상태" 의 대기열 순서대로 에이전트를 새로 띄운다 (각 대기열 항목 = 지시문 원본).
2. 워크트리 · 브랜치가 남아 있는지 `git worktree list` 로 확인. 합치기 대기 브랜치는 "합치기" 절 참고.

---

## ⏸ 현재 상태 (최신 2026-10-22 04:14 정리)
main `ba4033fb` (2026-10-22 arty3 병합, FormatVersion 50 · Replay 18). 옛 상태 (2026-10-21 02:25 · 밤 중단 · 저녁 저장 · 밤샘 큐 · 2026-10-18~19 기록) 원문 → `docs/legacy/HANDOFF_ARCHIVE_2026-10-22.md`.
에이전트 규칙: `tools/agent/AGENT_RULES.md` (한도 · 잠금 · 게이트 값은 규칙 파일이 최신).
**오늘 결정 · 대기열 전체 = `docs/ROADMAP_SHORT.md` (상세 · 사용자 원문 인용 = `docs/ROADMAP_LONG.md` [1] — 재개 시 SHORT 부터)**.

### WIP 브랜치 (kd-develop/kd-…) — 재개 시 새 에이전트
| 브랜치 | 커밋 | 다음 |
|---|---|---|
| `arty3` | ✅ 병합 완료 (2026-10-22, ba4033fb, FV 50 · Replay 18) | 방렬 지연 · 자리 유지 · heavy 간격 · 중기관총 32 · 야전포 간접 + 100 · 곡사 splash 3 + 조정 사격 · 최후 저지선 (ENGINE_NOTES arty3). 해석 · 추측 수치는 DECISIONS_PENDING |
| `pronemove` | ✅ 병합 완료 (2026-10-21, FV 48) | 교두보 집계에 Released 중대 포함 (TacticalAi.Crossing CpBridgehead) → crossing PASS. selftest 1905 OK |
| `units` | ✅ 병합 완료 (2026-10-21, Snapshot 47) | 선택: 기수 깃발 ×1.5 · 흔들기 4프레임 (-12/6/32/6°) · 카자크 = 러시아 계열 + 한국군 · 털모자 높임 + 털 질감 · 간도 의용병 1920년대 카키 복식. 그림 시트 docs/art_review/units_fix_cossack_gando.png (사용자 확인 대기) |
| `choke` | `efe5f98` WIP (기본 끔) | 대형 완화 묶음에 포함 (QUEUE 2번). 上 |

### 📋 2026-10-22 승인 대기열 (사용자 일괄 승인 — 슬롯 나는 대로 새 에이전트로 시작)
**한도**: 에이전트 20 · 上 6 · Gemini pro 3 + flash 3 · 무거운 작업 5 (임시, 주간 초기화 직전 몰아치기). 진행 중이면 중복 시작 금지.
**진행 중(2026-10-22 낮)**: gemini-import UI 병합 · 캠페인 WIP 검토 · 기마 포병 · 대형 완화+choke · 포병/중화기 · 돌격보병 강화 · 몽골/마적 외형 · 건물 크기→철도 지형(장갑열차 제외) · 참호 돌파(연막 · 철조망 · 경박격포 · 연막 수류탄) · 사격 통제/명령 대기열/사선 표시 · 전술 AI 3B · 기병 하마 사격/정찰병 · 튜토리얼 0-1/0-2 · 전 유닛 재렌더 · 저장 호환 정책 · '총기병'→'기병' · 신규 병종 N68/N71.
**승인·미착수 (순서 = 의존)**: 1) 사상자 속도 조정(돌격보병+대형 완화 병합 뒤) 2) 밤 soak 갱신→전투 문제 9건 재보고 3) trench_assault 하락 원인 조사(上) 4) S1~S5 5) 조작 UI 묶음(gemini-import 병합 뒤; 연대 선택 키는 플레이어 연대 편제 설계 포함) + UI 종이 질감 개편(우선, UI 묶음 뒤) 8) 공병(선로 파괴 등 남은 부분) 9) 통신·지휘 구조 알파(上) 10) 지휘 편의: 미션 중 저장·자동 저장 · 전과 보고 · 교전 알림/미니맵 12) 대포병 · 야간 전투 · 행군 피로 15b) 한국군 타찬카(기마 포병 병합 뒤) 16) N79 승인 항목 17) Major/Minor · 장교 학파/성향 구현 · 연대장 위임 18) 캠페인 홈 MVP(승인 범위) 19) 캠페인 규칙 1~15 구현 26) 엄폐물 군집 · 숲 모양 · 지형 세분화(건물 크기 병합 뒤) 29b) 소리: 무전 음성 교체 · 세력별 음성 방침 31) harness_local 구동 시험 · 문서/kd 폴더 정리 · CLAUDE.md selftest 수 갱신 30) 저장소 C:\works 이동(맨 마지막, 모든 에이전트 종료 뒤).
**보류**: 6b 장갑열차(철도 지형만 진행) · 7 하천 · 11 전술 AI 3C/3D+1만 부하(전부 끝난 뒤) · 28 지도 편집기 구현 · 20~22 캠페인 순서 연결/챕터 1/엔딩/북간도·체코 군단(추후 명령). 24 일본 군모 45식은 완료 여부 확인 필요(코디네이터 확인 중), 25 오스틴 완료(사용자).

### 📦 2026-10-22 최종 이관 (사용자 지시, **맨 마지막 작업**): game-tools · korean-division → `C:/gameProject/` + 새 .git
- 위치(사용자가 만들어 둠): `C:/gameProject/korean-division`, `C:/gameProject/game-tools` (C 드라이브 직속 — OneDrive 동기화 밖, 앞서 우려한 동기화 부담 해소). 워크트리(kd-develop · gt-develop)는 새 위치 아래 필요 시 재생성.
- 정정: 온라인 GitHub 의 FailingVillain 저장소는 단순 백업본이며, 사용자 요구는 **GitHub 에 올라간 그 저장소들을 지우는 것**(계정은 지우지 않음). GitHub 저장소 삭제는 사용자의 GitHub 로그인이 필요(Settings → Danger Zone → Delete this repository) — 이 PC 에는 삭제 도구/토큰이 없어 사용자가 직접 하며, 하신 뒤 알려 주면 확인. 로컬 옛 .git 삭제와 온라인 삭제가 모두 끝나면 옛 이력은 복구 불가(남는 것 = 새 스냅샷).
- 사용자 결정: 두 저장소 모두 **새 .git(커밋 1개 스냅샷, 이메일 seungwoo3656 만)**, 옛 .git(레거시 이메일) 로컬 삭제.
- **옛 이메일 로컬 잔존 금지(사용자 2026-10-22)**: `-old` 보관 없이, 새 저장소 검증 직후 옛 .git · 옛 작업 폴더 · 임시 번들/백업(C:/works/_git 포함 레거시 이력본) 즉시 삭제. 스냅샷 만들기 전에 문서/메모리/설정에서 레거시 주소 문자열을 grep 으로 전부 제거(HANDOFF · DECISIONS_PENDING · ROADMAP_SHORT · AGENT_RULES 등). 남는 곳: ~/.claude 세션 기록(jsonl)에는 명령 텍스트로 남을 수 있음 → 사용자가 원하면 해당 기록 삭제(사용자 몫).
- **이력 소실 보완**: 이관 직전에 `docs/legacy/GIT_HISTORY_DIGEST.md` 생성(병합 커밋 + 주요 커밋의 날짜 · 제목만, 이메일 · 작성자 제외) — 에이전트가 "이미 구현됐나"를 `git log` 대신 이 파일 Grep 으로 확인(N61/N63/N78 중복 조사 낭비 방지). BASELINE_CURRENT · SELFTEST_HISTORY · ROADMAP_DONE 은 유지.
- 선행 조건(순서): ① 모든 에이전트 종료 · 병합 큐 끝(미병합 브랜치는 이력 삭제 시 영구 손실) · 렌더 샤드 종료 ② 작업 폴더째 이동(추적 안 되는 산출물 포함: game-tools/korean-division-image-out · icons · audio-*, 게임 docs/soak · art_review 등) ③ 새 위치에서 git init + 단일 커밋 → 빌드 · detcheck · selftest 로 검증 ④ 검증 끝난 뒤에만 옛 .git 삭제(권한 필요 시 사용자 직접/허용 요청) ⑤ 절대 경로 갱신: AGENT_RULES(heavy_gate 경로 등) · CLAUDE.md · HANDOFF · 메모리 · publish.ps1 · 도구 스크립트(import_kd1920 의 game-tools 경로 등) · harness_local 설정 ⑥ dist 재발행 ⑦ 임시 내부 bare(C:/works/_git/*.git)는 새 GitHub 주소가 정해질 때까지 유지, push 는 사용자 주소 수령 후.

### 📧 2026-10-22 레거시 이메일 처리 (사용자 정정 — 이전 이관/삭제 계획은 **취소**)
- 사용자 정정: 레거시 주소(retired legacy school email)를 git 에서 지우라는 명령이 아니다. **이력 재작성 · 원격 저장소 삭제 · 계정 삭제 하지 않는다.** 요구는 하나: **레거시 이메일로 등록된 온라인 git(`FailingVillain/korean-division`)에 더 이상 push 하지 말 것**(korean-division 만, 더 이상 업데이트되지 않게). 새 커밋은 `seungwoo3656@gmail.com`.
- 조치(2026-10-22): korean-division 의 `origin` push URL 을 `DISABLED_legacy_email_no_push` 로 막음(`git remote -v` 로 확인 가능, fetch URL 은 그대로). 워크트리 모두 같은 설정 공유. 되돌리려면 `git remote set-url --push origin <URL>` — 사용자 명령 시에만.
- game-tools: 원격 push URL 도 차단(2026-10-22). 새 출발 = 현재 상태 1커밋 스냅샷을 임시 내부 bare 저장소 `C:/works/_git/game-tools.git` 에 올림(커밋 `d16163c`, 이메일 seungwoo3656 만). 작업 끝나면 스냅샷 갱신(`git archive HEAD` → 새 커밋) + 사용자가 새 GitHub 저장소 URL 을 주면 거기로 push.

### 🚀 2026-10-22 04:xx 속도 개선 5안 승인 + 한도 18/5
- 한도: 에이전트 18 · 上 5 · 무거운 작업 5(렌더 예약 1). 최우선 = 병합 잠금 해소 + 첫 병합.
- 적용: ① 병합 직전 검증 단축(빌드·detcheck·자기 묶음·snaptest; 전체 selftest 는 큐 마지막 AI 3B 병합자가 1회) ② 묶음별 시험 병렬 최대 2 ③ 병합 큐 종료 뒤 렌더 예약 3칸 ④ 큰 sim 작업 분할 병합(포병·참호 돌파) ⑤ 통합 밸런스 측정+soak 은 큐 종료 뒤 1회. 잠금 보유자는 `heavy_gate.ps1 -Priority`.

### 🧊 2026-10-22 도구 고정 (사용자 승인)
- agentHub · harness_local · 에이전트 템플릿 · 규칙 문서는 현 상태로 **고정**. 새 도구/규칙은 "이 도구 없이 최근 일주일에 몇 시간을 잃었나"에 구체적 숫자로 답할 수 있을 때만.
- 이번 일주일 기록 3가지(기존 로그/보고에서만): ① 게이트 대기 합계 시간 ② 병합 때마다 반복한 검증 횟수·시간 ③ Antigravity 플랜 대 API 평균 응답 시간(`agy_usage.jsonl`). 병합 큐 종료 뒤 "병합 직전엔 결정성+해당 시험, 전체 시험은 큐 끝에 한 번" 절차 개선 보고.
- 유일한 수정: agy_delegate 기본값(짧은 flash · 웹검색 = API 직행, 긴 입력 · pro · argon = 플랜).

### ✅ 2026-10-22 추가 승인 (역제안 A/B) — 대기열에 추가
- 추천안 "다음 한 걸음": ① **통합 밸런스 측정 1회** — 돌격보병 · 대형 완화 · 포병 · 기병 · 탄약 등 병합이 몇 개 끝난 뒤 A/B/C 시나리오 시드 8, 결과를 기준선으로 고정(`docs/BASELINE_CURRENT.md`) ② **결정 사항 한 장 정리** — N47 · N57 · n50 K/E 악화 수치 · 대형 완화 기본값 · 에이전트가 올린 질문을 모아 사용자에게 ③ **캠페인 수직 슬라이스** — 튜토리얼 0-1→0-2→1-1(압록강 도하)을 "미션 선택→편성→전투→결과→보충" 한 사이클로 끝까지, 그 뒤에 탭 확장. 먼저 Gemini 캠페인 WIP 의 **저장 트랜잭션(미션 진입 전 임시 상태 → 결과 확정 시 원자적 커밋)** 수정 + 캠페인 저장 `schemaVersion`/필드별 기본값 규칙.
- **난이도 구조 처음부터**(승인): 통신 현실성(쉬움/보통/어려움)과 같은 슬라이더 구조에 AI 정확도 · 탄약 · 증원도 묶음 — 통신 구조 알파 작업에 포함.
- **병합 큐**(승인): 병합 순서 돌격보병 → 대형 완화 → 포병 → 기병 → 참호 돌파 → 사격 통제 → AI 3B. 코디네이터가 순서 조정, 충돌 시 뒤 작업이 main 병합 후 해결.
- 그 외 "보완" · "뺄 것" 승인. "추가하면 좋을 것" = **설계 단계 → `docs/wishadd.md`** 에 보관, 해당 차례에 사용자에게 재보고 후 결정(구현 금지).
- 한도: 첫 병합 이후 에이전트 13 · 上 4 (진행 중 16/6 은 자연 감소 대기).

### 다음 순서 (QUEUE 번호)
- **(2026-10-21 추가) 캠페인을 게임에 넣는 첫 묶음 — 주간 한도 초기화 뒤**: ① 튜토리얼 0-1 (90 vs 마적 30, 50% 피해 시 퇴각 → 자동 승리) · 0-2 (약 600명, 다리 여러 개, 초소 1곳만 보임, 이벤트 2개: 마적 기병 30 · 서간도 의용군 40 합류, 산채 파괴 = 승리, 쓰러진 병사 50% = 패배) — 미션 파일 · 지도 생성 · 이벤트 스크립트 (中~上) ② 캠페인 순서: c1_01 회령 · c1_02 두만강 → WIP 보관, 튜토리얼을 앞에 ③ 캠페인 홈 MVP (`docs/CAMPAIGN_MVP_SPEC.md` S1~S5, 上 포함). 설계 = `game-tools/campaign/EPISODES.md` · `docs/CAMPAIGN_HOME_DESIGN.md` · `docs/CHARACTERS.md` · `docs/OFFICER_DOCTRINES.md`.

### 사용자 결정 대기 · 검수
- 시트: type3_hmg.png · austin_fix3.png (렌더 후) · progress_bar 는 게임에서 직접 (스크린샷에 안 찍힘).
- 세력 추가 후보 (적군/극동공화국 추천) · 저장소를 OneDrive 밖으로 옮길지 (bin 3.6 GB · kd-develop 1.8 GB 동기화 부담).
- 기존: N79a '후퇴' 버튼 유지? · 행진 사격 화염 다른 병종 · 캠페인 1-1/1-2 직접 플레이 · prone_batch / missing_theme_units / surrender / new_units 시트.

### 사용자에게 받을 결정 · 검수
- (2026-10-19 `retreat`) 중대 후퇴 임계를 사상률 30~40% 로 (retreatStrength 0.60~0.70, 데이터만). 미결: `retreatSuppression` (중대 평균 제압 0.003~0.02 라 사실상 발동 안 함), Spent 후 반감, 대대/연대 ×0.75 — 사용자가 제압 · 사기를 재설계할 때 함께 정함.
- 효과음 전체 청취 (`C:/gameProject/game-tools/korean-division-audio-default/index.html`).
- 일괄 재렌더 후 세력별 개요 · 슬림화 전후 비교 검수.
- 전술 AI 2단계 결과 · 새 밸런스 수치 제안 승인.

### 잔여 N 대기 (아카이브로 옮긴 원문 참조)
- N46 · N56 · N56b · N57 · N60 · N62 · N64 · N65 · N66 · N67 · N69 · N70 · N78 · N79 세부 · S1–S5 — 승인 대기열 · ROADMAP_SHORT 와 대조해 살릴 것만 남길 것.
