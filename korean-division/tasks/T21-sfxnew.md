# T21 sfxnew 복원 (효과음 연결)
- T21 | feature | D中 · H1 (R1 SM Wlight)
- 선행 없음 | 후행 없음
- 만질 파일: assets/sounds/kd1920/sounds.json, src/UI/SfxDirector.cs, docs/SOUND_POLICY_2026-10-22.md, docs/DECISIONS_PENDING.md (표현 전용, 시뮬 불변)
- 브랜치 sfxnew / kd-develop/kd-sfxnew | 상태: wip-ready | feature
## 확정 결정
WIP 병합 금지, wip-ready 까지만 (PIPELINE 정책 2026-10-22). 새 메카닉 없음.
## 범위 밖
breach 연동 키(mortar/smoke/wire)의 실제 연결(breach 병합 후), 새 음원 제작, 함성 방침 선택(사용자 결정).
## 수용 기준
빌드 통과, selftest cleanup/ai(BattleSelfTest) PASS, detcheck GOLDEN match.
## 측정
없음 (표현 전용).
## 결과
sfxnew.patch 복원 (DECISIONS_PENDING 는 3-way 충돌이라 한 줄 수동 추가). 엽총 shotgun, 타찬카/기마포병 wagon_loop, 기병 mount_switch 연결 + sounds.json 키 준비. 빌드 OK, cleanup·ai PASS, detcheck GOLDEN match. 청취 검수는 KD_SFX_BATTLE=1 에서 사용자 확인 필요.
