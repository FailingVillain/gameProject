@echo off
chcp 65001 >nul
rem 녹음 파일(m4a/wav/mp3) -^> outputs\soldier (군인 목소리) / outputs\radio (무전기). 조절값은 voice_fx.py 의 SETTINGS.
cd /d "%~dp0"
python voice_fx.py %*
pause
