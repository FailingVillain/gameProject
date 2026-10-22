# C1E1-u2 C1E1 누락 유닛 정의 5종 (봉천 기마 · 봉천 야포 · 일본 철도수비대 · 피난민 · 의용병)
- id: C1E1-u2 | 역할: feature | 유형: data | 모델: sonnet | 등급: D中 H1 (W light)
- 선행: C1E1-u1 (감사 docs/C1E1_UNIT_AUDIT.md) | 병렬/직렬: 병렬 (g5 피난민 정의를 이 카드가 먼저 함)
- 브랜치: kd/c1e1-u2-units (base: main d0fbc8f) | 상태: wip-ready | 담당 에이전트: feature (Sonnet)
## 범위
balance.json 에 유닛 정의만 추가 (기존 값 불변, 시뮬 코드 변경 없음, 새 그림 없음 — artAs 자리표시). 수치 = 가장 가까운 기존 유닛 복사 (밸런스 보류).
## 결과
- `cavalry_fengtian` 봉천 기마 (= cavalry_default, 봉천 전용, 그림 cavalry_default@fengtian) · `heavy_field_gun_fengtian` 봉천 야포 (= heavy_field_gun, 봉천 전용, 그림 야전포) · `infantry_railway_guard_japanese` 일본 철도수비대 (= infantry_japanese, 일본 전용, 그림 infantry@japanese) · `civilian_refugee` 피난민 (공용, 무기·근접 없음, hp 60, 속도 2.6, 점령·자동 사격 없음, 제압 1.5, 그림 infantry) · `common_volunteer` 의용병 (공용, = 민단 자위대 수치, 그림 infantry_reserve; 열린 점 해소 — 임시 `common_volunteer_tmp` 와 별개로 둠).
- 시험: units 그룹에 C1E1 블록 10건 (정의 로드, 세력별 AllowedFor, artAs 실재, MissionDef 편성 통과/거부). units / factions(57) / missions(98) / savecompat(15) PASS, detcheck GOLDEN 일치 (8aa6448b).
- BalanceHash: 유닛 정의 추가로 Defs.Hash 가 바뀜 — 기록 재생·스냅샷 로드는 경고만 (Replay.cs:179, Snapshot.cs:280), 실패 없음.
## 엔진 카드가 필요한 것 (이 카드는 만들지 않음)
- 피난민: 도주·공포 AI 없음, "비전투" 전용 플래그 없음 (무기 없음 + autoAttack/canCapture false 로만 표현). TacticalAi SkipUnarmed 가 무기 없는 유닛을 AI 에서 제외. 호송 목표(escort)·피난민 70% 도착/50% 사망 판정은 g4/g5.
- 철도수비대: 부속지가 별도 중립 세력 슬롯이면 그 슬롯 En 이 japanese 여야 편성 가능 (factions japanese). 중립·비적대·무장해제/억류 규칙은 엔진 카드.
- 봉천 기마 SK4 −40%/+20% 증감·브리핑 오정보 표시는 미션 JSON/엔진 쪽. 봉천 야포 포병 승무원·진강산 배치는 미션 카드.
- 새 그림 5종(전용 스프라이트)은 재렌더 뒤 아트 카드.
