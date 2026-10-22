# I18N 영문 번역 현황 (단일 관리 문서)

> 기준일: 2026-10-23. 이 문서는 영문 번역 관련 사항을 한 곳에서 관리한다. 코드 · 데이터는 바꾸지 않는다.
> 상태 표기: **확정** = 사용자 결정 / **제안** = 사용자 확인 대기 / **확인 필요** = 문서에 근거 없음.

---

## ① 방침

### 기준 문서

| 문서 | 위치 | 비고 |
|---|---|---|
| 영문 용어집 | `docs/I18N_GLOSSARY.md` | 메인 작업 트리에는 없음. 브랜치 `kd/ti5-en-draft` 에 있음 (worktree `C:/gameProject/kd-develop/kd-ti5`). 기계 검사는 `assets/i18n/glossary.csv` 가 담당하며 두 파일은 같이 바꾼다. |
| 영문 파이프라인 | `docs/I18N_ENGLISH_PIPELINE.md` | **확인 필요** - 어느 브랜치에도 해당 파일이 확인되지 않음. |
| TI5 검토 목록 | `docs/I18N_REVIEW_TI5.md` | 브랜치 `kd/ti5-en-draft` (worktree `C:/gameProject/kd-develop/kd-ti5`). 메인 작업 트리에는 없음. 번역 판단이 갈리는 문구의 사용자 검수 목록. |
| 영문 데이터 | `assets/i18n/en/*.csv` (`key,ko,en,status,kohash`) | 브랜치 `kd/ti5-en-draft` 에 정규 키 315행 (status=draft). TI1 기록 (브랜치 `kd/ti1-i18n-core`): `ui_misc.csv` 시범 3행. 메인 작업 트리에는 `assets/i18n/en` 이 확인되지 않음. |

### 번역 규칙 (용어집 §7 에서 옮김)

- 영어는 ASCII 출력 문자만 쓴다 (글꼴 안전). 앞뒤 공백 · 겹공백 · 한글 금지.
- 같은 용어는 한 가지 표기만 쓴다. 자리표시 `{0}`, `|`, 줄바꿈 수는 원문과 같게 한다.
- 약어 금지: `Maj. Gen.` `Col.` `Lt.` `Capt.` `Sgt.` `Pvt.` `Bn.` `Regt` `Coy` `Plt` `Div.`. `versus` 대신 `vs.`.
- 금지 표기: `brigand` / `outlaw` (마적 = bandits), `Mukden` (봉천 = Fengtian), `Jiandao` / `Kando` / `Chientao` (간도 = Gando).
- 지명 · 인명은 개정 로마자 (한 번만 정함). 인명은 성-이름 순서 유지 (서양식 순서 안 씀), 이름은 하이픈 (`Kim Ju-hyeon`).
- 문장 속 `—` 는 ` - `, 이름 · 제목 · 장소의 `·` 는 ` / `.
- 번역 금지 (확정, 카드 TI5 · 설계서 §1): 창 제목 `KOREAN DIVISION : MARCH ON MANCHURIA`, 메뉴 한글 제목 `대한 사단 : 북벌 1920`.

---

## ② 확정된 번역 결정 (사용자 확정)

| 항목 | 한국어 | 영문 (확정) | 적용 범위 |
|---|---|---|---|
| 성씨 | 이씨 | Lee | Lee Tae (황손 이태), Lee Seok (황자 이석) |
| 지명 | 간도 | Gando | 서간도 = Western Gando |
| 지명 | 봉천 | Fengtian | Mukden 금지 (세력명과 일관) |
| 직위 | 황손 | Imperial Grandson | `event.e4_heir_lecture.*` 등 |
| 직위 | 황자 | Imperial Prince | |
| 부대 | 시위대 | Royal Guard | 기존 TI4 번역과 같음 |
| 사건 제목 | 공적 가로채기 | Merit Stolen | `e2_credit_steal.title` |
| 인명 표기 | (전체) | 성-이름 순서 유지, 개정 로마자, 이름 하이픈 | 예: Kim Ju-hyeon |
| 자원 · 상태 | 심기 | mood | `황손 심기` = `Imperial Grandson's mood` |
| 자원 · 상태 | 신임 | confidence | `총사령관 신임` = `Commander-in-Chief's confidence` |

출처: 사용자 지시 (2026-10-23 세션). 용어집 §4 · §5b · §6 과 맞춤.

---

## ③ 열린 항목 (사용자 확인 대기)

열린 항목 합계: **11건** + 번역 에이전트 결과 판단 갈림 건 (집계 확인 필요).

### 3-1. 번역 에이전트 신규 189키 중 판단 갈림 건

- 상태: 번역 진행 중. 완료되면 판단 갈림 건이 `docs/I18N_REVIEW_TI5.md` 에 추가된다 (브랜치 `kd/ti5-en-draft`).
- 링크: `docs/I18N_REVIEW_TI5.md` (브랜치 `kd/ti5-en-draft`, 메인 작업 트리에는 없음).
- 집계: **확인 필요** - 189키 결과 파일이 아직 확인되지 않음. 현재 검수 목록은 315키 기준 (A 표기 결정 / B 번역 선택 / C 자리표시 / D 길이 / E 범위 밖).

### 3-2. 연대장 성향 8종 영어 표기 (제안)

한국어 원문 라벨은 저장소에서 확인되지 않았다 (**확인 필요**). 아래는 ID 의미에 따른 영어 후보 2개씩이며, 채택 전 사용자 확인이 필요하다.

| ID | 한국어 라벨 | 영어 후보 1 | 영어 후보 2 | 비고 |
|---|---|---|---|---|
| offense | 확인 필요 | Aggressive | Offensive-minded | 공세 (`commander_tags.json` 에 '공세' 태그 있음, 대응 확인 필요) |
| caution | 확인 필요 | Cautious | Prudent | |
| nerve | 확인 필요 | Steady Nerves | Composed | |
| humane | 확인 필요 | Humane | Merciful | `trait.caring` = Benevolent 와 겹치지 않게 |
| logistics | 확인 필요 | Logistics-minded | Supply-focused | `trait.quartermaster` = Quartermaster 와 겹치지 않게 |
| artillery | 확인 필요 | Artillery-minded | Fire-Support Minded | `trait.gunner` = Gunner 와 겹치지 않게 |
| command | 확인 필요 | Command-minded | Strong Command | |
| discipline | 확인 필요 | Disciplined | Stern | |

- 참고: 저장소 데이터 `assets/data/commander_tags.json` (브랜치 `kd/ti5-en-draft`) 의 태그 어휘는 공세 · 수비 · 인명보존 · 보급중시 · 포로관대 · 포로엄격 · 학파존중 · 시위대편애 · 속전속결 · 민간보호 10개이며, 위 8종 ID 와 1:1 대응하지 않는다. 8종의 근거 문서는 확인되지 않음.
- 기존 특성 영어 (확정 아님, TI5 초안): 철벽 Iron Wall · 돌격 정신 Assault Spirit · 인자 Benevolent · 포병통 Gunner · 보급통 Quartermaster · 노련 Veteran.

### 3-3. 귀화자 연대장 표기 (제안)

| 한국어 | 제안 영문 | 대안 | 출신 (확정 근거) |
|---|---|---|---|
| 볼로디미르 이바노프 | Volodymyr Ivanov (?) | 확인 필요 | 우크라이나 (`docs/SETTING_NOTES.md`: 오극란 근위연대 = 잔류 백계 러시아인 중 우크라이나계 주축) |
| 얀 카친스키 | Jan Kaczynski (?) | 확인 필요 | 폴란드 (`docs/SETTING_NOTES.md`: 파란도 장교단 = 연해주 출신 폴란드인) |

- 출신 영문 표기 (origin 필드): Ukraine / Poland - 제안.
- 로마자 `(?)` 표시는 사용자 확인 전 임시. 디아크리틱 (ń, ł) 은 ASCII 규칙에 따라 뺀다 (제안).

### 3-4. 외국 실존 장군 인명 표기 규칙

- 상태: **미정** (사용자 결정 필요). 규칙 후보 (확인 필요): (a) 원어 로마자 성-이름 / (b) 한국 문헌 표기 음역 / (c) 직책만 표기.
- 현재 저장소에 해당 규칙 문서 없음 (**확인 필요**).

### 열린 항목 합계

| 구분 | 건수 |
|---|---|
| 3-2 연대장 성향 8종 | 8 |
| 3-3 귀화자 연대장 | 2 |
| 3-4 외국 실존 장군 규칙 | 1 |
| 3-1 189키 판단 갈림 | 집계 확인 필요 |
| **합계 (집계 가능분)** | **11** |

---

## ④ 영어 행이 아직 없는 신규 문자열 (브랜치별)

조사 방법: 각 브랜치의 `docs/decisions_pending.d/*.md` · `docs/engine_notes.d/*.md` 에서 '영어' · 'English' · 'i18n' 언급을 찾고, 메인 대비 공통 fragment 를 확인함. 8개 대상 브랜치 모두 같은 i18n fragment 세트를 가진다 (브랜치마다 고유한 번역 fragment 는 없음).

| 브랜치 | 공통 i18n fragment (확인됨) | 신규 한국어 문자열 수 | 키 위치 |
|---|---|---|---|
| kd/objective-hud-audit | `kd__ti1-i18n-core.md`, `kd__ti4-data-text.md`, `kd__ti4-followups.md`, `kd__episode-i18n-fix.md` | 확인 필요 | 확인 필요 |
| kd/undo-ground | 위와 동일 | 확인 필요 | 확인 필요 |
| kd/autopause-core | 위와 동일 (`kd__autopause-core.md` 는 자동 일시정지 건, 영어 무관) | 확인 필요 | 확인 필요 |
| kd/commander-fields | 위와 동일 | 확인 필요 | 확인 필요 |
| kd/ui-lowfix | 위와 동일 (`kd__ui-lowfix.md` 포함) | 확인 필요 | 확인 필요 |
| kd/tpow-a-loot | 위와 동일 (`kd__tpow-a-loot.md` 포함) | 확인 필요 | 확인 필요 |
| kd/ckpt-preop | 위와 동일 | 확인 필요 | 확인 필요 |
| kd/rv2-replay-followup | 위와 동일 (`kd__rv1-replay-controls.md`, `kd__rv2-replay-followup.md` 포함) | 확인 필요 | 확인 필요 |

공통으로 확인된 영어 미연결 사항 (브랜치 공통):

- `kd__episode-i18n-fix.md`: 에피소드 탭 서사 키 노출 수정. 신규 한국어 문자열 = `"에피소드 C1E1"` (제목), `"노드: 미션 <미션id>"` (미션 머리). 서사 본문 / 결과 줄은 삭제. 에피소드 S 문자열은 한국어 const 그대로 (영어 키 없음). 키 위치: 확인 필요 (fragment 에 파일 경로 없음).
- `kd__ti1-i18n-core.md` (engine_notes): `src/Core/Loc.cs`, `tools/i18n/convert_consts.py` 로 `S.*` const 를 `Loc.T(key, 원문)` 으로 전환. 시범 `StringsCampaignPrisoners.cs` 13개만 변환. 나머지 `Strings*.cs` 는 TI2 대기 (**영어 행 없음**).
- `kd__ti4-data-text.md` (engine_notes): 미션 / 캠페인 이벤트 / 연대장 / 특성 JSON 옆 영어 CSV 방식. 사건 메시지 `m.Messages` 는 시뮬이 한국어 원문 저장 → 표시 때 `DisplayMessage` 로 키 조회. 키 위치: `assets/i18n/en/mission_<id>.csv`, `data_events.csv`, `data_commanders.csv`. 영어 행 현황은 브랜치 `kd/ti5-en-draft` 기준 (메인에는 없음).
- 후크 미구현 (영어 행 없음): 계급 (일본군 · 봉천군 · 마적 계급 영어는 제안만), 학파, 날씨 표시 이름, `SnapMsg`, 세력 `factions[].name`, `mapData.name`.

---

## ⑤ 갱신 절차

새 판단 갈림 문구는 담당 번역 에이전트가 `docs/I18N_REVIEW_TI5.md` (브랜치 `kd/ti5-en-draft`) 에 추가하고, 사용자가 확정하면 이 문서 ②에 옮긴 뒤 ③ 에서는 '확정 · 날짜'로 표시한다 (삭제 금지).

---

## 변경 이력

- 2026-10-23: 최초 작성 (방침 · 확정 결정 · 열린 항목 · 브랜치별 신규 문자열 표). 커밋 · push 안 함.
