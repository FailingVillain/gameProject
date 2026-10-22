# T30b 장갑열차 무기 · 객차별 피해/파괴 — T30a 위 스택
- id: T30b | 역할: sim | 등급: D上 H3 (R3 S=M W=heavy)
- 선행: T30a | 후행: T30c T30e
- 핫스팟: Defs(객차 정의·무기), balance.json, WorldCombat/Combat 피해 경로(객차 장갑), Snapshot(FV+1), Audit, SelfTest, 사상자 로그
- 브랜치 `t30b-train-weapons` (t30a-train-move 1800d5a + 리뷰 수정 863f9d1 체리픽 위, 워크트리 kd-develop/kd-t30b) | 상태: wip-ready | 담당: sim (Opus) | FV 59 · Replay 그대로 (명령 추가 없음)
## 확정 결정
TRAIN_PLAN: 장갑열차 = 야포 + 중기관총, 중장갑, 객차 단위 파괴.
## 범위 밖
선로 파괴, 폭발물 신규 종류, 객차 연쇄 폭발·탈선 등 새 메카닉, 열차 AI, 새 포탄 종류(기존 야포/기관총 정의 재사용).
## 수용 기준
1. 포차 = 기존 야전포 무기, 기관총차 = 기존 중기관총 무기를 객차 정의로 재사용; 사격/조준은 기존 전투 경로.
2. 각 객차 개별 HP · 장갑; 0 되면 그 객차만 파괴(파괴 객차는 잔해 상태로 남아 선로 위 정지 or 분리 = Q-B1 결정대로).
3. 대전차소총/포 대 장갑 값 판정이 기존 장갑차(tank_armored_car) 규칙과 일관 – 시험으로 확인.
4. 같은 시드 2회 체크섬 동일, 저장/복원 왕복, Audit 0, 체크섬 – 열차 없는 지도 불변.
## 측정
SelfTest + snaptest + golden; 소규모 교전 시험(보병 중대 vs 장갑열차 1대 시드 8) 결과를 카드 결과에 기록(승패 비율만, 밸런스 조정은 범위 밖).
## 질문
- Q-B1 선두/중간 객차가 파괴되면 열차 이동은? (권장: 기관차 파괴 시 전체 정지, 나머지는 잔해로 남음. 분리 이동은 신규 규칙이라 금지)
- Q-B2 객차별 무기 구성(포 몇 문·기관총 몇 정)과 HP/장갑 수치? 에이전트 제안 후 사용자 확인.
- Q-B3 객차 내 승무원 개념은 쓰는가? (권장: 없음, 객차 HP 로만)
## 결과
- 권장안 채택 (사용자 승인 대기): Q-B1 기관차(engine) 모두 파괴 → 열차 전체 제동 · 정지 · 명령 무시, 다른 객차는 잔해(HP 0)로 열차에 남아 끌려감 (분리 없음). Q-B3 승무원 없음 (객차 HP 만).
- Q-B2 제안값 (balance.json trains.cars): 기관차 hp 400 heavy(3) engine · 포차 hp 300 heavy, gun_field 1문 · 기관총차 hp 300 heavy, hmg 2정 · 화차 hp 200 basic. 모두 category armored (장갑차 규칙), sight 24. 객차 무기 = 탄약 무제한 · 사계 360° · 직사만 (야전포 관측 간접 사격 없음).
- 잔해 = 시뮬 상태(Alive false, TrainCar.Wreck); 그림 연결(T30d train-wreck)은 T30e. 수류탄 · 착검은 객차에 안 통함 (근접 = 장갑 불가, 수류탄 = 유닛만 — 장갑차 묶음 수류탄 규칙은 미적용).
- 구현: 전투 경로 공유 — FindTarget 객차 후보(유닛 뒤 · 건물 앞) · Fire/FireSpray 사격자 Entity · 병과 bonus CategoryOf · Explode 반경 SplashTrains · World.Find 객차 대비 · 안개 객차 시야. 저장: 열차 구역 ORG_ 끝 → ENTS 끝 (유닛 표적 참조가 객차를 찾도록), 엔티티 참조 태그 3 = 객차. 체크섬 · 검증(무기 키 · hp · armor · 타이머) T30a 리뷰 방식대로.
- 시험: KD_SELFTEST_ONLY=trainweapons 19검사 PASS (정의 · 소총 면역 · 대전차소총 1발 32 = 10×3.5−3 · 장갑차와 같은 공식 · 근접 불가 · 객차 사격 40초 기관총 190발/포 착탄 3 · 잔해 · 기관차 정지 · 교전 중 저장 왕복 바이트 동일 + 3000틱 체크섬 · 결정성). train · replay · snapshot(Audit) · savecompat · missions · shells · combat4 · armour · pursuit PASS, snaptest OK, detcheck GOLDEN match.
- 측정: 열차 3대(무기 없음) 틱 비용 −0.65% (잡음). 소규모 교전 보병 중대(장교 1 · 소총 24 · 대전차소총 4) 공격 이동 vs 장갑열차(기관차 · 포차 · 기관총차 · 화차), 시드 1~8 × 3분: 열차 승 8 · 보병 승 0 · 미결 0 (보병 전멸 8/8). 밸런스 조정은 범위 밖 — 사용자 확인 때 수치 재검토 필요.
