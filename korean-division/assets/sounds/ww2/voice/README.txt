세력별 보이스 (음성 대사) 폴더 안내
=====================================

세력마다 서로 다른 언어의 목소리가 나온다. 어느 언어를 쓰는지는 지도 파일(assets/maps/*.json)의
세력 정의 "lang" 값이 정한다 (생성 맵 · 교전 테스트는 en).

  corners4 맵 기본값
    공화국 (Republic)  = en  (영어)
    연방   (Union)     = en  (영어)
    왕국   (Kingdom)   = de  (독일어)
    소비에트 (Soviet)  = ru  (러시아어)

폴더 구조
---------
  assets/sounds/ww2/voice/
    en/   영어
    ru/   러시아어
    de/   독일어
    (새 언어는 폴더를 만들고 지도 파일에 "lang": "<코드>" 만 쓰면 된다 - 예: fr, ja)

파일 이름 규칙
--------------
  <사건>_<번호>.wav      예)  select_1.wav, select_2.wav, select_3.wav, under_attack_1.wav

  - 번호는 1부터 붙이는 "변형" 이다. 같은 사건에 여러 개를 넣으면 무작위로 하나를 고르고,
    직전에 쓴 것은 되도록 피한다. 변형 수에 제한은 없지만 사건당 3~6개를 권장.
  - 파일이 하나도 없으면 그 사건은 조용히 넘어간다 (오류 없음). 일부만 채워도 된다.
  - 확장자는 .wav 권장 (.ogg 도 읽는다). 대소문자 구분 없음.
  - 해당 세력이 "내 세력" 일 때만 재생된다 (내 유닛 선택 · 내 명령 · 내 유닛이 공격받음 / 전사 등).
    관전 · 리플레이 · AI 세력의 사건에서는 재생되지 않는다.

사건 목록
---------
  사건 이름          언제 나오나                                       같은 세력 최소 간격
  select             내 유닛을 선택했을 때                              0.9 초
  move               이동 · 순찰 명령을 내렸을 때                       0.9 초
  attack             공격 · 어택땅 명령을 내렸을 때                     0.9 초
  under_attack       내 병력 · 기지가 공격받을 때 ("경보" 효과음과 함께) 10 초
  unit_ready         유닛 생산이 끝났을 때                              2.5 초
  death              내 유닛이 전사했을 때 (여러 기가 죽어도 한 번)      4 초
  building_complete  건설이 끝났을 때                                   3 초
  capture            내가 시설(마을 · 광산 · 유전 · 도시)을 점령했을 때   4 초

  그 밖에도 한 세력의 대사 사이에는 최소 0.7 초 간격이 있고, 이전 대사가 아직 재생 중이면
  새 대사는 건너뛴다 (대사가 겹치거나 도배되지 않게).

권장 녹음 사양
--------------
  - 형식: WAV, 44.1 kHz, 모노(1채널), 16-bit PCM
  - 음량: 최대 피크 -3 dB 이하 (정규화 후 -3 dB), 파일끼리 체감 음량을 맞출 것
  - 길이: 짧게. select / move / attack = 0.3 ~ 1.2 초, unit_ready / building_complete / capture = 0.6 ~ 1.5 초,
          under_attack = 0.8 ~ 2.0 초, death = 0.3 ~ 1.0 초 (비명 · 짧은 외침)
  - 앞뒤 무음은 0.05 초 이내로 잘라낼 것 (반응이 느려 보이지 않게), 끝은 10 ms 정도 페이드 아웃
  - 무전기 · 라디오 느낌을 주려면 300 Hz ~ 3.4 kHz 대역 통과 + 약한 노이즈를 입혀도 좋다 (선택)

대사 예시 (사건별 · 언어별)
---------------------------
  select (선택)
    en: "Ready.", "Yes, sir?", "Awaiting orders.", "Reporting."
    ru: "Слушаю!", "Готов.", "Есть!", "На связи."
    de: "Bereit.", "Zu Befehl!", "Ja, Herr Kommandant?", "Wir hören."

  move (이동)
    en: "Moving out.", "On our way.", "Understood.", "Roger that."
    ru: "Выдвигаемся.", "Есть, иду!", "Понял.", "Вперёд!"
    de: "Wir rücken vor.", "Verstanden.", "Jawohl!", "Auf dem Weg."

  attack (공격)
    en: "Engaging!", "Attack!", "Open fire!", "For the Republic!"
    ru: "В атаку!", "Огонь!", "За Родину!", "Урааа!"
    de: "Angriff!", "Feuer frei!", "Vorwärts!", "Für das Königreich!"

  under_attack (공격받음)
    en: "We're under attack!", "Contact! We need support!", "Taking fire!"
    ru: "Нас атакуют!", "Контакт! Нужна помощь!", "Под огнём!"
    de: "Wir werden angegriffen!", "Feindkontakt! Verstärkung!", "Wir stehen unter Beschuss!"

  unit_ready (생산 완료)
    en: "Unit ready.", "Reinforcements have arrived.", "Ready for duty."
    ru: "Подкрепление прибыло.", "Боец готов.", "К службе готов."
    de: "Einheit bereit.", "Verstärkung ist eingetroffen.", "Dienstbereit."

  death (전사)
    en: (짧은 비명) "Argh!", "I'm hit!", "Medic..."
    ru: (짧은 비명) "А-а!", "Меня ранило!", "Санитар..."
    de: (짧은 비명) "Argh!", "Getroffen!", "Sanitäter..."

  building_complete (건설 완료)
    en: "Construction complete.", "Building finished."
    ru: "Строительство завершено.", "Здание готово."
    de: "Bau abgeschlossen.", "Gebäude fertiggestellt."

  capture (점령)
    en: "Position secured.", "Objective captured."
    ru: "Позиция занята.", "Объект захвачен."
    de: "Stellung gesichert.", "Ziel eingenommen."

확인 방법
---------
  1) 위 규칙대로 wav 를 넣고 게임을 (다시) 실행한다. 파일은 처음 필요할 때 읽는다.
  2) corners4 에서 소비에트(--faction soviet) 로 플레이하면 ru 폴더의 대사가 나오는지 확인.
  3) 콘솔(Enter)의 "mute" / "volume N" 으로 전체 음량(효과음 + 보이스)을 조절할 수 있다.

[임시 적용 — 2026-10-18]
  지금 들어 있는 음성은 임시다: 사용자가 녹음한 보병 대사 4개(언어별)를 game-tools/audio-voice 의 voice_fx.py 로
  무전기(radio) 변조한 것을 select / move / attack 세 사건에 똑같이 변형 1~4 로 넣었다.
  원본: coding/game-tools/audio-voice/outputs/radio/[EN|GR|RU]-Inf-Voice-<n>.wav  (GR = de 폴더)
  정식 대사(사건별 녹음)가 오면 이 파일들을 교체할 것.

[2026-10-19] 음성 파일 제거 (사용자 지시: 나중에 넣음). 파일이 없으면 PlayVoice 는 조용히 무음. 옛 임시 음성은 git 기록 (커밋 01ee886 이전) 에 있음.
