"""혼성 대 소총병: python tools/snrain_mix.py "infantry_sniper:40,infantry:160" "infantry:200"  (시드1~5 x 교대) """
import subprocess,re,sys,os
EXE=os.path.join(os.path.dirname(__file__),'..','bin','snrain','KoreanDivision.exe')
X,Y=sys.argv[1],sys.argv[2]; xw=yw=0;lx=ly=0;T=0;n=0
for seed in range(1,6):
  for swap in (0,1):
    a=(X,Y) if not swap else (Y,X)
    p=subprocess.run([EXE,'--balance','sim',a[0],a[1],'flat',str(seed),'1','500'],capture_output=True,text=True,encoding='utf-8',errors='replace')
    m=re.search(r'MEAS (.*)',p.stdout)
    if not m: print('FAIL',p.stdout[-200:]);continue
    kv=dict(t.split('=') for t in m.group(1).split()); le=[int(v) for v in kv['lossEnd'].split('/')]; w=int(kv['winner'])
    x,y=(le[0],le[1]) if not swap else (le[1],le[0])
    xw+=(w==(2 if swap else 1)); yw+=(w==(1 if swap else 2)); lx+=x; ly+=y; T+=float(kv['end_t']); n+=1
print(f'{X} v {Y}: Xwin {xw} Ywin {yw} of {n}; loss X {lx/n:.0f} Y {ly/n:.0f} exch(Y/X) {ly/max(lx,1):.2f} t {T/n:.0f}')
