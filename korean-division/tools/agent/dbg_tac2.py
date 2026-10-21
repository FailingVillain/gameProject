p='src/Tests/TacticalSelfTest.cs'
s=open(p,encoding='utf-8').read()
old='        // 곡사포: 장교가 보는 적에게만 (관측 전용) — 방열 후 포격\n'
new=old+'''        if (Environment.GetEnvironmentVariable("KD_SEEDS")=="1") for (int sd = 1; sd <= 10; sd++) { var a2 = BattleWorld("infantry:120,officer:6,heavy_howitzer:3", "infantry:130,officer:4", seed: sd); var g2 = a2.Units.Where(u => u.Owner == 1 && u.Def.Key == "heavy_howitzer").Select(u => u.Id).ToHashSet(); int im2 = 0; for (int i = 0; i < 150 * 60 && a2.Winner == 0; i++) { a2.Step(); foreach (var im in a2.Impacts) if (im.Tick == a2.Tick - 1 && g2.Contains(im.ShooterId)) im2++; } Console.WriteLine($"SEED {sd} impacts={im2} gunsAlive={a2.Units.Count(u => g2.Contains(u.Id) && u.Alive)} inf1={a2.Units.Count(u=>u.Alive&&u.Owner==1&&u.Def.Key=="infantry")} inf2={a2.Units.Count(u=>u.Alive&&u.Owner==2&&u.Def.Key=="infantry")}"); }
'''
assert old in s
s=s.replace(old,new,1)
open(p,'w',encoding='utf-8').write(s)
