# Self-contained distribution (card A1)

Problem: Debug/dev builds (`bin/main/KoreanDivision.exe`) are framework-dependent apphosts. In the test environment they cannot find the .NET runtime (run as `dotnet KoreanDivision.dll` instead). For shipping, use the self-contained folder build.

## Usage
```
powershell -ExecutionPolicy Bypass -File tools/publish_selfcontained.ps1 [-Out dist-sc] [-Verify] [-ConsoleExe] [-NoR2R]
```
- Output `dist-sc\KoreanDivision.exe` (win-x64, runtime included, no .NET install needed) + `dist-sc\assets\`. Copy the whole folder.
- `-Verify` runs `--detcheck` on the output exe and compares with `assets/data/detcheck.golden` (first hash).
- `-ConsoleExe` builds a console-subsystem exe (default is WinExe, no console window; redirected stdout still works).
- Uses its own `obj/sc/`; does not touch `dist\` (the older single-file `publish.ps1`) or `bin\`. `dist-sc/` is git-ignored.

## Choices
- Folder form, not single-file: assets (135 MB) and `replays\` live beside the exe (`AppContext.BaseDirectory`), single-file extraction would complicate that.
- Trimming off (reflection JSON loading). ReadyToRun on (startup); detcheck unaffected (golden matches).

## Measured (2026-10-22, main 423f327 base)
- Size: 210.2 MB total = assets 129.1 MB + runtime/app 81.1 MB, 2360 files.
- `--detcheck` on published exe: `8aa6448b9690b922 262d0252c35087e2` = golden.
- `KD_SELFTEST_ONLY=snapshot --selftest` on published exe with dotnet removed from PATH/DOTNET_ROOT: SELFTEST PASS (~56 s).

## Known limits
- win-x64 only. No installer / no code signing (SmartScreen will warn). Options if wanted: zip the folder, or Inno Setup / MSIX (proposal only).
- Cross-variant detcheck (tools/detcheck.ps1) still targets bin/ exes; pass `-Exe dist-sc/KoreanDivision.exe` to use the published one.
- Raylib native DLL ships as a loose file in the folder.
