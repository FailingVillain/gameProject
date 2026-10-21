# 부대 편제 (중대 · 대대) + 깃발 대형 이동 — 구현 계획 (✅ 2026-10-18 구현 완료, 실제 동작 · 차이는 ROADMAP §2-7 · ENGINE_NOTES 부대 편제 절)

> 사용자 지시로 **장교 · 제압 기능 뒤로 미룸**. 코드 분석과 설계까지 끝낸 상태. 재개 시 이 문서와 `docs/ROADMAP.md` §2-4b "부대 편제 2단계" 를 따른다.
> 이 브랜치에 실제로 들어간 코드: `--stress J` 시나리오 (100기 덩어리 횡대 행군, 성능 기준값 측정용) 뿐.

## 성능 기준값 (변경 전, `--stress J k=2000,8000 ticks=600 players=16`, map 280²)
| K | avg ms/틱 | p95 | 경로 계산 | 지역 흐름장 |
|---|---|---|---|---|
| 2000 | 2.19 | 3.97 | 836회 (0.15 ms) | 340회 |
| 8000 | 7.48 | 9.06 | 4025회 (0.70 ms) | 1105회 |
구현 뒤 같은 명령으로 다시 재서 비교 (중대 깃발 1개 경로 → 경로 계산 횟수가 크게 줄어야 한다).
`StressTest.FormCompanyForStress` 는 `static partial void` — 편제 구현 파일(`StressTestOrg.cs`)에서 `w.FormCompany(1, list)` 를 호출하도록 채운다 (지금은 아무것도 안 함 → J = 옛 FormPlan 횡대 이동).

## 1. 시뮬레이션 자료구조 (새 파일 `src/Core/Organisation.cs`, `src/Core/OrgFormation.cs`)
- `Company` (소부대): `Id, Owner, Number(제N중대 — 세력 안 비어 있는 가장 작은 번호), FormType Form(기본 Line=2열), InitialStrength, Vector2 Facing(마지막 도착 방향), List<Unit> Members(Id 오름차순), Battalion?, CompanyAnchor? Anchor`. 최대 100기, 유닛은 한 중대에만 (`Unit.Company` 역참조 추가).
- `Battalion` (대부대): `Id, Owner, Number(1~9, 0 = 번호 없음 → 0 목록으로만), BatForm Form, List<Company> Companies(≤10)`.
- `enum BatForm { Line, Column, Echelon, TwoFrontReserve }` — 대대 대형 (중대 블록 배치). 중대 대형은 기존 `FormType` 재사용 (Line = 2열 횡대, Column, Spread = 산개; Free 는 Box 로 취급).
- `CompanyAnchor` (깃발): `Pos, Heading(단위벡터), Goal, FinalDir, Arrived, Settled, Column(좁은 길 모드), ClearSince, HalfWidth, Spacing, Slots(유닛Id→로컬 오프셋 X=오른쪽 Y=앞), ColumnSlots, FinalOff(도착용 세계 오프셋 — ShiftSlot 적용), MoveGroup Group, Unit Flag`.
  - **Flag = 가상 Unit** (World.Units 에 없음, Id = 1_000_000 + 중대 Id, Def = 가장 느린 구성원 (Large 가 있으면 가장 느린 Large)). 기존 `MoveToward/RouteDirection` 을 그대로 재사용해 경로를 1개만 계산. `MoveToward` 에 `speedMul`, `checkProgress` 선택 인자 추가.
- World: `Companies`, `Battalions`, `nextCompanyId`, `nextBattalionId`. 유닛 사망(`RemoveDead`) · 세력 이전(`TransferUnitsCommand`) 시 `LeaveCompany`; 빈 중대 · 빈 대대는 즉시 제거. 자동 재편성 없음.

## 2. 명령 (GameCommand.cs 에 JsonDerivedType 등록 + WorldCommands.Execute 분기)
`FormCompanyCommand(Units)` · `DisbandCompanyCommand(CompanyId)` · `SplitCompanyCommand(CompanyId)` (Id 순 앞 절반 유지, 뒤 절반 새 중대 — 같은 대대에 자리 있으면 편입) · `FormBattalionCommand(Companies)` (기존 대대에서 빼낸 뒤 번호 = 비어 있는 1~9, 없으면 0) · `SetBattalionNumberCommand(BattalionId, Number)` (이미 쓰면 교환) · `MoveCompanyCommand(CompanyId, BattalionId)` (0 = 대대에서 빼기, 10개면 거부) · `SetCompanyFormCommand` · `SetBattalionFormCommand`.
- `FormSpec` 에 `float Width = 0` 추가 (우클릭 드래그 폭, 옛 리플레이 호환). `NormForm` 은 Free 라도 Dir/Width 가 있으면 null 로 만들지 않는다.
- `ExecGroupOrder`: 중대 소속 유닛은 중대별로 묶어 `ExecCompanyOrder`, 나머지는 기존 경로. Patrol 은 기존 방식 유지 (깃발 미적용 — 알려진 제한).

## 3. 깃발 이동 알고리즘
- `ExecCompanyOrder`: 방향 = `form.Dir` (우클릭 드래그) → 없으면 모든 중대의 `Facing` 이 같으면 그 값(현재 방향 유지) → 아니면 무게중심→목표를 **8방향으로 스냅** (`Snap8`). 중대 블록 = `CompanyBlock(units, form, 폭/나란한 중대 수)`; 대대 대형으로 블록 중심 배치 (`BattalionLayout`, 간격 2칸, 중대 순서 = 현재 좌우 투영 순 → 교차 방지); 블록마다 `StartCompanyMove`.
- `StartCompanyMove`: 그룹 생성 (Plan 없음, Inited=true, Big=false), 깃발 시작점 = 선택 구성원 무게중심(ShiftSlot), 초기 heading = 목표 방향, 슬롯 배정 = 시작점 기준 세계 슬롯에 `Formations.Match` (internal 로 공개), `FinalOff` = 도착점 기준 ShiftSlot. `Company.Facing = dir`.
- `StepAnchors()` (Step 에서 UpdateUnit 전, Companies 순서): 따라오는 구성원이 없으면 Anchor 제거. 최대 지연(lag) = 구성원과 슬롯 거리 최대 → `>2.5칸 대기(0배)`, `>1.2칸 0.5배`, 아니면 1배. 깃발을 `MoveToward(flag, Goal, Goal, false, dt, mul)` 로 전진, heading 은 속도 방향으로 부드럽게 (벡터 보간, 삼각함수 없음). 도착하면 `Arrived`, heading 을 FinalDir 로 돌린 뒤 `Settled`. flag.StuckLevel ≥ 4 면 현 위치에서 도착 처리.
- **좁은 길**: 10틱마다 `Map.ClearLine(Pos ± right·HalfWidth)` 과 3칸 앞 같은 선을 검사. 막히면 `Column` (2열 종대, 현재 위치 기준 재배정); 60틱 연속 트이면 원래 대형으로. 슬롯 칸이 못 서는 칸이면 깃발 위치로 (개별 길찾기 → 지나간 뒤 재정렬).
- `FollowGroup` (WorldNav): `u.Company?.Anchor.Group == u.Group` 이면 `FollowAnchor`: 행군 중 슬롯 = `Pos + right·X + Heading·Y`, 거의 닿았으면 제자리(CheckProgress 생략 — 정체 오판 방지), navGoal = 깃발 위치를 4×4 로 양자화(경로 · 흐름장 공유), steer = 슬롯. 도착 후엔 `Goal + FinalOff` 로 기존 도착 판정 그대로, Facing = FinalDir.
- 옛 `Formations.Build` 도 Dir 없으면 8방향 스냅 (사선 대형 수정) → 골든 체크섬 갱신 필요 (`--detcheck --update`).

## 4. 저장 · 체크섬
- 스냅샷 새 구역 `ORG_` (CMDX 뒤, END_ 앞). 버퍼는 `WriteLinks` 직후 · `WriteGroups` 전에 만든다 (깃발 유닛의 그룹 · 경로 참조가 표에 번호를 받도록). 내용: 카운터, 대대(Id Owner Number Form), 중대(Id Owner Number Form InitialStrength Facing 대대Id 구성원Id들, Anchor 유무 + 전체 필드 + 세 슬롯 사전(키 정렬) + 깃발 유닛(Id, DefKey, Pos) + `WriteUnitLinks`). `WriteLinks/ReadLinks` 의 유닛 1기 부분을 `WriteUnitLinks/ReadUnitLinks` 로 분리해 깃발에도 사용.
- `Snapshot.FormatVersion` 3 → 4. `SnapshotSelfTest.Audit` 에 Company · Battalion · CompanyAnchor 추가, Unit 에 `Company`, World 에 `Companies Battalions nextCompanyId nextBattalionId`.
- `ComputeChecksum` 에 `HashOrganisation` (대대 · 중대 · 구성원 Id · 깃발 Pos/Heading/Goal/Arrived/Column).

## 5. UI (Game.cs · Hud.cs · Strings.cs)
- `ControlGroups.cs` 제거 (기본 대형 Type/Order 만 Game 필드로 유지, `CombatSetting.DefaultFormation` 연동). `FactionsSelfTest`(170~184) · `FormationSelfTest.TestControlGroupForms` 도 교체.
- 키: **Alt+숫자** 중대 편성(FormCompany) · **숫자 1~9** 대대 선택(연타 = 카메라) · **0** 대대 목록 패널 토글 · **Ctrl+숫자** 선택한 대대 번호 변경 · **G** 병합(FormBattalion) → 격자는 **Ctrl+G** 로 이동 · Shift+숫자(부대 추가) 삭제 · Ctrl+F 중대 대형 순환, Ctrl+Shift+F 대대 대형 순환.
- 마우스: **Shift+드래그** = 중대 단위 선택 (상자 안 유닛의 중대 전체) · **우클릭 드래그** = 전선 (방향 = 드래그에 수직, 선택 무게중심 반대쪽; 폭 = 드래그 길이(칸) → `FormSpec.Width`), 드래그 중 슬롯 미리보기 (`OrgLayout.PreviewSlots`, 순수 함수).
- 부대 막대 → 대대 1~9 (대대 대형 단추). 하단 정보 패널: 대대 선택 시 중대 카드(번호/제N중대, 병과 아이콘 = 다수 유닛 Def 로 `UnitRenderer.DrawBody`, 병력 막대 현재/초기, 탄약 막대 평균 `AmmoRatio`, 제압 표시는 `Unit` 에 `Suppression` 멤버가 있으면(리플렉션) 표시 아니면 숨김). 카드 클릭 = 중대 선택(Shift 추가), 상세 영역 [분할] [해산] [대형], 카드 드래그 → 대대 칸/0 목록에 놓으면 MoveCompany. [병합] 버튼.
- 문자열: `제{0}중대`, `제{0}대대`, 병합 · 분할 · 해산, 키 도움말 Hint3 갱신.
- 렌더: 이동 중인 내 중대 깃발 표시 (작은 깃발 + 번호), 우클릭 드래그 미리보기.

## 6. 테스트 (FormationSelfTest 갱신 + 추가)
행군 중 대형 유지 (직선 행군 중 슬롯 편차 최대값) · 우클릭 드래그 방향/폭 (ranks = width/간격) · 중대/대대 소속 규칙 (100 · 10 · 하나만 · 빈 부대 소멸 · 번호 자동/교환) · 편제 + 깃발 스냅샷 왕복 · 좁은 길 종대 전환 후 재정렬 · 리플레이 체크섬 일치. 검증: `--selftest`, `--snaptest`, `--detcheck --update`, `--uifuzz 60 --novsync`, `--stress J k=2000,8000`.
