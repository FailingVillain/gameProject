# T36 면역 표적 제외 (자동 표적 · 반격)
- T36 | sim | D中 H1 (WorldCombat 2줄)
- 선행: T30b (스택) | 후행: -
- 핫스팟: WorldCombat.cs IsValidTarget · Retaliate, TrainWeaponsSelfTest, detcheck.golden
- 브랜치 kd/immune-target-fix (kd-t30b 워크트리) | 상태 wip-ready | sim(Sonnet)
## 확정 결정
소총 등이 피해를 줄 수 없는(면역) 표적에게 반격/자동 표적하지 않는다. 플레이어 공격 명령은 그대로. 밸런스 값 변경 금지.
## 수용 기준
CanDamage(CategoryOf(t)) 거짓이면 Retaliate 무시, IsValidTarget 무효 (Order==Attack 제외). selftest T36 3건.
## 결과
trainweapons/train/combat4/armour PASS, detcheck 8변형 동일. 골든 갱신(정당): 소총병이 장갑 표적에 반격하지 않아 시나리오 units 20→31 (8aa6448b.. → fcf38519..). 8-0 측정: 변화 없음 (열차 8승, 보병 전멸; 지시로 심층 측정 생략).
