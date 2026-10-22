using System.Buffers.Binary;
using KoreanDivision.Core;

namespace KoreanDivision.Tests;

/// <summary>
/// 저장 형식 호환 정책 (ENGINE_NOTES 저장 호환): 옛/새 형식 · 잘린 · 빈 · 임의 바이트 저장과 리플레이는 예외(크래시) 없이
/// SnapshotException / InvalidDataException 으로 사유를 돌려준다. KD_SELFTEST_ONLY=savecompat.
/// </summary>
public static class SaveCompatSelfTest
{
    public static void Run(GameOptions opt, Action<bool, string> check)
    {
        var defs = Defs.Current;
        var setup = new BattleSetup { SideA = "infantry:10", SideB = "infantry:10", Map = "flat", Distance = 40, AiVsAi = true, AiA = "battle", AiB = "battle" };
        var w = setup.CreateWorld(defs, 3);
        for (int i = 0; i < 120; i++) w.Step();
        var good = Snapshot.Save(w, new SnapshotMeta("호환", 1, false, "battle"));

        // 정상 파일은 열린다 (대조군)
        bool ok = false;
        try { ok = Snapshot.Load(good, defs).World.Tick == w.Tick; } catch (Exception) { }
        check(ok, "저장 호환: 현재 형식 저장은 열린다");

        // 옛 형식 · 새 형식 머리말 (형식 번호만 바꿈): 사유 메시지에 저장 · 현재 버전과 안내 문구
        int cur = Snapshot.FormatVersion;
        foreach (var (fmt, word) in new[] { (cur - 1, "이전 버전"), (1, "이전 버전"), (cur + 1, "새로운 버전"), (65535, "새로운 버전") })
        {
            var b = (byte[])good.Clone();
            BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(4, 2), (ushort)fmt);
            string m1 = Reject(() => Snapshot.Load(b, defs));
            string m2 = Reject(() => Snapshot.ReadHeader(b));
            string tmp = TestTemp.Child("kd_compat.kds");
            File.WriteAllBytes(tmp, b);
            string m3 = Reject(() => Snapshot.ReadHeaderFile(tmp));
            string m4 = Reject(() => Snapshot.LoadFile(tmp, defs));
            File.Delete(tmp);
            bool all = new[] { m1, m2, m3, m4 }.All(m => m.Contains(word) && m.Contains("열 수 없습니다") && m.Contains($"v{fmt}") && m.Contains($"v{cur}"));
            check(all, $"저장 호환: 형식 v{fmt} 저장 거부 (Load · ReadHeader · ReadHeaderFile · LoadFile) — {m1}");
        }

        // 잘린 파일 (여러 길이) · 빈 파일 · 임의 바이트: 예외 종류가 SnapshotException 하나뿐
        int wrong = 0, rejected = 0; string first = "";
        void Probe(string what, byte[] data)
        {
            try { Snapshot.Load(data, defs); wrong++; first = first == "" ? what + " 정상 로드됨" : first; }
            catch (SnapshotException e) { rejected++; if (string.IsNullOrEmpty(e.Message)) { wrong++; first = what + " 빈 메시지"; } }
            catch (Exception e) { wrong++; first = first == "" ? $"{what}: {e.GetType().Name}" : first; }
        }
        Probe("빈 파일", Array.Empty<byte>());
        foreach (int n in new[] { 1, 3, 4, 5, 6, 9, 10, 11, 50 }) Probe($"앞 {n}바이트", good.AsSpan(0, Math.Min(n, good.Length)).ToArray());
        for (int i = 1; i <= 40; i++) Probe($"잘림 {i}/41", good.AsSpan(0, (int)((long)good.Length * i / 41)).ToArray());
        Probe("한 바이트 모자람", good.AsSpan(0, good.Length - 1).ToArray());
        var rng = new Random(12345);
        for (int i = 0; i < 60; i++)
        {
            var junk = new byte[rng.Next(0, 4000)];
            rng.NextBytes(junk);
            if (i % 3 == 1 && junk.Length >= 4) "KDSV"u8.CopyTo(junk);   // 매직만 맞는 쓰레기
            if (i % 3 == 2 && junk.Length >= 6) { "KDSV"u8.CopyTo(junk); BinaryPrimitives.WriteUInt16LittleEndian(junk.AsSpan(4, 2), (ushort)cur); }   // 형식까지 맞는 쓰레기
            Probe($"임의 {i}", junk);
        }
        check(wrong == 0 && rejected > 90, $"저장 호환: 빈 · 잘린 · 임의 바이트 {rejected}건 모두 SnapshotException 으로 거부, 엉뚱한 예외 · 정상 로드 {wrong}건 {first}");

        // 파일 경로로도: 빈 파일 · 쓰레기 · 잘림 · 없음
        string dir = TestTemp.Child("kd_compat_files");
        Directory.CreateDirectory(dir);
        string empty = Path.Combine(dir, "empty.kds"), junkF = Path.Combine(dir, "junk.kds"), cut = Path.Combine(dir, "cut.kds");
        File.WriteAllBytes(empty, Array.Empty<byte>());
        File.WriteAllBytes(junkF, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 });
        File.WriteAllBytes(cut, good.AsSpan(0, good.Length / 2).ToArray());
        int fileBad = 0;
        foreach (var f in new[] { empty, junkF, cut, Path.Combine(dir, "missing.kds") })
        {
            try { Snapshot.ReadHeaderFile(f); fileBad++; } catch (SnapshotException) { } catch (FileNotFoundException) { } catch (Exception) { fileBad++; }
            try { Snapshot.LoadFile(f, defs); fileBad++; } catch (SnapshotException) { } catch (Exception) { fileBad++; }
        }
        check(fileBad == 0, $"저장 호환: 파일 경로 읽기 (빈 · 쓰레기 · 잘림 · 없음) 가 SnapshotException 으로만 실패 (벗어난 경우 {fileBad})");

        // 리플레이 (.kdr.json): 현재 형식 왕복, 옛/새 형식 · 빈 파일 · 잘린 JSON · 쓰레기
        string rp = Path.Combine(dir, "ok.kdr.json");
        ReplayFile.FromWorld(w).Save(rp);
        bool rOk = false;
        try { rOk = ReplayFile.Load(rp).Format == ReplayFile.FormatVersion; } catch (Exception) { }
        check(rOk, "저장 호환: 현재 형식 리플레이는 열린다");
        string json = File.ReadAllText(rp);
        int rcur = ReplayFile.FormatVersion;
        bool vOk = true; string vmsg = "";
        foreach (int fmt in new[] { rcur - 1, 1, rcur + 1 })
        {
            string p = Path.Combine(dir, $"v{fmt}.kdr.json");
            string edited = json.Replace($"\"Format\":{rcur}", $"\"Format\":{fmt}").Replace($"\"format\":{rcur}", $"\"format\":{fmt}");
            File.WriteAllText(p, edited);
            string m = RejectReplay(p, out var ex);
            vmsg = m;
            vOk &= edited != json && ReplayVersionException.Is(ex) && m.Contains("열 수 없습니다") && m.Contains($"v{fmt}") && m.Contains($"v{rcur}") && m.Contains(fmt < rcur ? "이전 버전" : "새로운 버전");
        }
        check(vOk, $"저장 호환: 옛/새 형식 리플레이는 버전 불일치 예외로 거부 — {vmsg}");
        int rBad = 0; string rFirst = "";
        void RProbe(string what, string content)
        {
            string p = Path.Combine(dir, "probe.kdr.json");
            File.WriteAllText(p, content);
            RejectReplay(p, out var ex);
            if (ex is not InvalidDataException) { rBad++; rFirst = rFirst == "" ? what + ": " + (ex?.GetType().Name ?? "정상 로드") : rFirst; }
        }
        RProbe("빈 파일", "");
        RProbe("공백", "   ");
        RProbe("쓰레기", "\u0001\u0002\u0003 not json");
        RProbe("배열", "[1,2,3]");
        RProbe("null", "null");
        RProbe("형식 문자열", "{\"Format\":\"x\"}");
        for (int i = 1; i <= 20; i++) RProbe($"잘림 {i}", json[..(int)((long)json.Length * i / 21)]);
        check(rBad == 0, $"저장 호환: 빈 · 잘린 · 쓰레기 리플레이가 InvalidDataException 으로만 실패 (벗어난 경우 {rBad}) {rFirst}");

        // 캠페인 저장 (campaign.json): schemaVersion · 필드 기본값 규칙
        var okc = CampaignState.Parse("{\"Version\":1,\"NextMission\":\"c1_02_tumen\",\"Completed\":[\"c1_01_hoeryong\"]}", out var e0);
        check(okc != null && e0 == null && okc.Completed.Count == 1 && okc.Units.Count == 0 && okc.EntryMission == "" && !okc.HowitzerCompany && okc.Version == CampaignState.SchemaVersion,
            "캠페인 저장: 일부 필드만 있는 파일은 모자란 필드를 기본값으로 채워 연다");
        var noVer = CampaignState.Parse("{\"NextMission\":\"x\"}", out var e1);
        var unk = CampaignState.Parse("{\"Version\":1,\"someFutureField\":[1,2]}", out var e2);
        check(noVer != null && e1 == null && unk != null && e2 == null, "캠페인 저장: 버전 칸 없음 → 기본 버전, 모르는 필드는 무시");
        var newer = CampaignState.Parse($"{{\"Version\":{CampaignState.SchemaVersion + 1}}}", out var e3);
        check(newer == null && e3 != null && e3.Contains("더 새로운 버전") && e3.Contains($"v{CampaignState.SchemaVersion + 1}") && e3.Contains($"v{CampaignState.SchemaVersion}"), $"캠페인 저장: 더 새 스키마는 거부 — {e3}");
        int cBad = 0; string cFirst = "";
        foreach (var t in new[] { "", "   ", "garbage", "[]", "null", "{\"Version\":0}", "{\"Version\":-3}", "{\"Version\":\"x\"}", "{\"Units\":null}", "{\"Completed\":5}", "{\"Version\":1,\"History\":[{\"DurationSeconds\":\"a\"}]}", "{\"Version\":1" })
        {
            try { var r = CampaignState.Parse(t, out var er); if (r != null || string.IsNullOrEmpty(er)) { cBad++; cFirst = cFirst == "" ? t : cFirst; } }
            catch (Exception ex) { cBad++; cFirst = cFirst == "" ? t + " " + ex.GetType().Name : cFirst; }
        }
        check(cBad == 0, $"캠페인 저장: 빈 · 쓰레기 · 잘못된 형식은 예외 없이 한국어 사유와 함께 거부 (벗어난 경우 {cBad}) {cFirst}");
        CampaignSlotsSelfTest.Run(check);   // T19a 슬롯 9칸 · 자동 저장 · 실패 재시도
        CampaignAmmoSelfTest.Run(check);    // T19b 구경별 탄약 · 포탄 (저장 v3 · 적재 · 발사 차감 · 노획)
        CampaignHomeSelfTest.Run(check);    // T19c 연대 배속 (Q9) 저장 왕복 · 투입 비용 공용 함수
        CampaignFatigueSelfTest.Run(check);   // T42a 피로 · 사기 계정 (저장 v4 · 정산 · 홈 요약)
        CampaignPromotionSelfTest.Run(check);   // T42b 진급 후보 · 적용 (저장 v5 · 후보 · 정원 · 이월)
        CampaignPrisonersSelfTest.Run(check);   // T42c 포로 장부 · 송환/수용 (저장 v6 · 정산 · 처리)
        CampaignScheduleSelfTest.Run(check);   // T42d 일정 · 연표 (저장 v7 · 일수 · 이벤트 기록 · 표시)
        CommanderDataSelfTest.Run(check);   // T48a 연대장 데이터 · 로더 · 저장 v8 · 풀 동기화
        TolerantReadSelfTest.Run(check);   // T49b 설정 / 사용자 작성 지휘관 JSON 관대한 읽기
        CampaignHomeViewsSelfTest.Run(check);   // T38f 읽기 전용 탭 (기록 · 장비 · 편제) 표시 자료
        string cdir = TestTemp.Child("kd_compat_campaign");
        string? oldEnv = Environment.GetEnvironmentVariable("KD_CAMPAIGN_DIR");
        Environment.SetEnvironmentVariable("KD_CAMPAIGN_DIR", cdir);
        try
        {
            Directory.CreateDirectory(cdir);
            File.WriteAllText(CampaignState.FilePath, $"{{\"Version\":{CampaignState.SchemaVersion + 1}}}");
            bool rej = CampaignState.Load() == null && CampaignState.LastLoadError != null;
            File.WriteAllText(CampaignState.FilePath, "{\"NextMission\":\"c1_02_tumen\",\"Completed\":[\"a\"]}");
            bool acc = CampaignState.Load() is { } cs && cs.NextMission == "c1_02_tumen" && CampaignState.LastLoadError == null;
            check(rej && acc, "캠페인 저장: 파일 경로 Load 가 새 스키마는 거부(사유 기록), 옛/부분 파일은 연다");
        }
        finally { Environment.SetEnvironmentVariable("KD_CAMPAIGN_DIR", oldEnv); try { Directory.Delete(cdir, true); } catch (IOException) { } }

        // 읽기 실패 파일 보호: 메뉴 저장 관문 · 결과 확정 (GameMission) 가 원본을 덮어쓰지 않는다 + 손상 목록 원소는 거부 + 탄약 배율 정수 계산
        {
            int nullBad = 0;
            foreach (var t in new[] { "{\"Roster\":[null]}", "{\"Units\":[null]}", "{\"History\":[null]}", "{\"Completed\":[null]}", "{\"Stock\":{\"RecentLog\":[null]}}", "{\"Pending\":{\"MissionId\":\"x\",\"DeployedCompanyIds\":[null]}}" })
            {
                try { var r = CampaignState.Parse(t, out var er); if (r != null || string.IsNullOrEmpty(er)) nullBad++; }
                catch (Exception) { nullBad++; }
            }
            check(nullBad == 0, $"캠페인 저장: 목록 안 null 원소는 크래시 없이 거부 (벗어난 경우 {nullBad})");

            string gdir = TestTemp.Child("kd_compat_guard");
            string? oldG = Environment.GetEnvironmentVariable("KD_CAMPAIGN_DIR");
            Environment.SetEnvironmentVariable("KD_CAMPAIGN_DIR", gdir);
            try
            {
                Directory.CreateDirectory(gdir);
                string brokenText = $"{{\"Version\":{CampaignState.SchemaVersion + 1},\"Keep\":\"original\"}}";
                File.WriteAllText(CampaignState.FilePath, brokenText);
                var menuState = CampaignState.Load() ?? new CampaignState();
                string? le = CampaignState.LastLoadError;
                bool menuBlocked = le != null && !menuState.SaveUnlessLoadFailed(le) && File.ReadAllText(CampaignState.FilePath) == brokenText;
                var won = new MissionResults { MissionId = CampaignState.FirstMission, Title = "1-1", Result = "won" };
                bool commitBlocked = !CampaignState.CommitResult(won) && File.ReadAllText(CampaignState.FilePath) == brokenText;
                check(menuBlocked && commitBlocked, "캠페인 저장: 읽기 실패 (더 새 버전) 동안 메뉴 저장 · 결과 확정이 원본을 덮어쓰지 않는다");
                bool moved = CampaignState.BackupBroken() && !File.Exists(CampaignState.FilePath) && File.ReadAllText(CampaignState.FilePath + ".bak") == brokenText;
                check(moved, "캠페인 저장: 초기화 전 원본은 campaign.json.bak 로 보존");
                CampaignState.Load();   // 파일 없음 → 거부 아님
                bool commitOk = CampaignState.LastLoadError == null && CampaignState.CommitResult(won) && CampaignState.Load() is { } saved && saved.Completed.Contains(CampaignState.FirstMission);
                check(commitOk && new CampaignState().SaveUnlessLoadFailed(null), "캠페인 저장: 파일이 없으면 (정상) 결과 확정과 메뉴 저장이 동작");
            }
            finally { Environment.SetEnvironmentVariable("KD_CAMPAIGN_DIR", oldG); try { Directory.Delete(gdir, true); } catch (IOException) { } }

            check(World.ScaleAmmo(100, 0.5f) == 50 && World.ScaleAmmo(7, 0.5f) == 4 && World.ScaleAmmo(100, 2f) == 100 && World.ScaleAmmo(100, -1f) == 0
                && World.ScaleAmmo(100, float.NaN) == 100 && World.ScaleAmmo(100, float.PositiveInfinity) == 100 && World.ScaleAmmo(1000, 0.333f) == 333,
                "미션 탄약 배율: 정수 천분율 (NaN · 무한대는 배율 없음, 범위 0..1)");
        }

        try { Directory.Delete(dir, true); } catch (IOException) { }
    }

    static string Reject(Action a)
    {
        try { a(); return "(거부되지 않음)"; }
        catch (SnapshotException e) { return e.Message; }
        catch (Exception e) { return "(엉뚱한 예외 " + e.GetType().Name + ")"; }
    }

    static string RejectReplay(string path, out Exception? ex)
    {
        try { ReplayFile.Load(path); ex = null; return "(거부되지 않음)"; }
        catch (InvalidDataException e) { ex = e; return e.Message; }
        catch (Exception e) { ex = e; return "(엉뚱한 예외 " + e.GetType().Name + ")"; }
    }
}
