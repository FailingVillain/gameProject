# T37 대리 지휘관 지연 단계 일치
- T37 | sim | D中 H1 (CommandDelay.cs)
- 선행: T24 (스택) | 후행: -
- 핫스팟: CommandDelay.cs ChainTierOf, CommandSelfTest, detcheck.golden
- 브랜치 kd/acting-delay-tier (kd-t24 워크트리) | 상태 wip-ready | sim(Sonnet)
## 확정 결정
CommandLevelOf 가 대리(Nco)로 보는 중대는 지연 단계도 Nco. 지연 핫패스에서 매 틱 ActingNco 호출 금지 (중대별 틱 캐시, 저장 안 함).
## 결과
command/cavcmd/comms PASS, detcheck 8변형 동일. 골든 갱신(정당): 분대에 부사관이 없어도 대리가 있으면 Nco 지연 (Acting 보다 짧음) -> 시나리오 units 156->69 (ccfbd1dd.. -> b9659b01..). 밸런스 값 변경 없음.
