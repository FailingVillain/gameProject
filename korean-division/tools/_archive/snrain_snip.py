"""sniper 교환비 측정: python tools/snrain_snip.py <nSniper> <nRifle> [rifleType] ; 시드1~5 x 좌우교대"""
import subprocess,re,sys,os
EXE=os.path.join(os.path.dirname(__file__),'..','bin','snrain','KoreanDivision.exe')
ns,nr=sys.argv[1],sys.argv[2]; ry=sys.argv[3] if len(sys.argv)>3 else 'infantry'
sw=0;rw=0;ls=0;lr=0;T=0;n=0
for seed in range(1,6):
  for swap in (0,1):
    a=(f'infantry_sniper:{ns}',f'{ry}:{nr}'); a=a if not swap else a[::-1]
    p=subprocess.run([EXE,'--balance','sim',a[0],a[1],'flat',str(seed),'1','400'],capture_output=True,text=True,encoding='utf-8',errors='replace')
    m=re.search(r'MEAS (.*)',p.stdout)
    if not m: print('FAIL');continue
    kv=dict(t.split('=') for t in m.group(1).split()); le=[int(x) for x in kv['lossEnd'].split('/')]; w=int(kv['winner'])
    s,r=(le[0],le[1]) if not swap else (le[1],le[0])   # sniper loss, rifle loss
    sw+= (w==(2 if swap else 1)); rw+=(w==(1 if swap else 2)); ls+=s; lr+=r; T+=float(kv['end_t']); n+=1
print(f'sn{ns} v {ry}{nr}: sniperWin {sw} rifleWin {rw} of {n}; loss S {ls/n:.1f} R {lr/n:.1f} exch(R lost/S lost) {lr/max(ls,1):.2f} t {T/n:.0f}')
