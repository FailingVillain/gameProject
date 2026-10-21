@echo off
chcp 65001 >nul
rem 유닛 초상화 생성기 (SD.Next 사용). 프롬프트는 prompts.json 에서 고친다.
cd /d "%~dp0"
python portrait_maker.py %*
pause
