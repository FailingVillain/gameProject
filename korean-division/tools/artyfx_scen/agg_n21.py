import json,re,sys,glob,os,statistics as st
def mean(l): return sum(l)/len(l) if l else float('nan')
def art(path):
    for line in open(path,encoding='utf-8',errors='replace'):
        if line.startswith('ARTYFX '): return json.loads(line[7:])
def sections(path):
    txt=open(path,encoding='utf-8',errors='replace').read()
    parts=re.split(r'^## 세력 (\d)',txt,flags=re.M)
    out={}
    for i in range(1,len(parts),2): out[parts[i]]=parts[i+1]
    return out
def bagg(d,label):
    rows=[]
    for s in range(1,25):
        p=f'{d}/b_s{s}.txt'
        if not os.path.exists(p): break
        rows.append((art(p),sections(p)))
    if not rows: return
    wins=sum(1 for a,_ in rows if a['winner']==1)
    K=[a['startA']-a['endA'] for a,_ in rows]; E=[a['startB']-a['endB'] for a,_ in rows]
    n=[a['n20']['1'] for a,_ in rows]
    pc=[];pi=[]
    for a,sec in rows:
        m=re.search(r'보병 첫 사격 전 포병 사격 (\d+), 접촉 전 (\d+)',sec.get('1',''))
        if m: pi.append(int(m.group(1))); pc.append(int(m.group(2)))
    hk=[a['gun'].get('1:heavy_howitzer',{}).get('killCredit',0) for a,_ in rows]; hs=[a['gun'].get('1:heavy_howitzer',{}).get('shells',0) for a,_ in rows]
    print(f"== B {label} (seeds 1-{len(rows)}): KR wins {wins}/{len(rows)}, t {mean([a['t'] for a,_ in rows]):.0f}, K {mean(K):.0f} / E {mean(E):.0f}, KR preContact impacts {sum(x['preShells'] for x in n)} (contact {mean([x['contact'] for x in n]):.0f}s), shells before first advance {mean([x.get('prepShells',0) for x in n]):.1f}/game (advance {mean([x.get('advance',-1) for x in n]):.0f}s), obs-tool shots before inf fire {sum(pi)} / before contact {sum(pc)}, observers alive {sum(x['obsAlive'] for x in n)}/{sum(x['obs'] for x in n)}, howitzer shells/game {mean(hs):.0f} kills/game {mean(hk):.1f}, gunMoves {mean([x['gunMoves'] for x in n]):.1f}")
def dagg(d,label):
    rows=[]
    for s in range(1,4):
        p=f'{d}/d_s{s}.txt'
        if not os.path.exists(p): return
        rows.append((art(p),sections(p)))
    wins=sum(1 for a,_ in rows if a['winner']==1)
    K=[a['startA']-a['endA'] for a,_ in rows]; E=[a['startB']-a['endB'] for a,_ in rows]
    for f in ('1','2'):
        n=[a['n20'][f] for a,_ in rows]
        artm=[];fr=[]
        for a,sec in rows:
            t=sec.get(f,'')
            m=re.search(r'포병 이동 (\d+)회',t); artm.append(int(m.group(1)) if m else 0)
            m=re.search(r'표적 보유 중 사격률 ([\d.]+)%',t)
            if m: fr.append(float(m.group(1)))
        hk=[sum(g['killCredit'] for k,g in a['gun'].items() if k.startswith(f+':')) for a,_ in rows]
        print(f"== D {label} F{f} (seeds 1-3): KR wins {wins}/3, t {mean([a['t'] for a,_ in rows]):.0f}, K {mean(K):.0f}/E {mean(E):.0f}, howitzer moves/game {mean(artm):.1f}, gun move-starts/game {mean([x['gunMoves'] for x in n]):.1f}, gun-sec moving {100*mean([x['gunMoveSec']/max(1,x['gunSec']) for x in n]):.0f}%, fire ratio {mean(fr):.1f}%, all-gun kill credit/game {mean(hk):.0f}")
def cagg(d,label):
    R={1:{},2:{}}
    rows=[]
    if not os.path.exists(f'{d}/c_s1.txt'): return
    for s in range(1,6):
        p=f'{d}/c_s{s}.txt'
        a=art(p); sec=sections(p)
        rows.append((a,sec))
    print(f'== C {label} (seeds 1-5)')
    wins=sum(1 for a,_ in rows if a['winner']==1)
    K=[a['startA']-a['endA'] for a,_ in rows]; E=[a['startB']-a['endB'] for a,_ in rows]
    print(f"  KR wins {wins}/5, mean t {mean([a['t'] for a,_ in rows]):.0f}s, K {mean(K):.0f} / E {mean(E):.0f}, K/E {mean(K)/mean(E):.2f}")
    for f in ('1','2'):
        n=[a['n20'][f] for a,_ in rows]
        pre=[x['preShells'] for x in n]; mv=[x['gunMoves'] for x in n]; ms=[x['gunMoveSec']/max(1,x['gunSec']) for x in n]
        obs=sum(x['obs'] for x in n); oal=sum(x['obsAlive'] for x in n); od=[x['obsDeathMean'] for x in n if x['obsDeathMean']>=0]
        ctc=[x['contact'] for x in n]
        asl=[];aon=[];artm=[];fr=[];far={};shots={};
        for a,sec in rows:
            t=sec.get(f,'')
            m=re.search(r'\[포병\] 돌격 (\d+)회 판정.*?조준점이 돌격 표적 12칸 안 (\d+)',t)
            if m: asl.append(int(m.group(1))); aon.append(int(m.group(2)))
            m=re.search(r'포병 이동 (\d+)회',t); artm.append(int(m.group(1)) if m else 0)
            m=re.search(r'표적 보유 중 사격률 ([\d.]+)%',t)
            if m: fr.append(float(m.group(1)))
            for m in re.finditer(r'\[포종\] (\S+): 사격 (\d+), 평균 거리 ([\d.]+), 사거리 80% 초과 (\d+)',t):
                shots[m.group(1)]=shots.get(m.group(1),0)+int(m.group(2)); far[m.group(1)]=far.get(m.group(1),0)+int(m.group(4))
        kc={}; first={}; sh={}
        for a,_ in rows:
            for k,g in a['gun'].items():
                if k.startswith(f+':'):
                    kc.setdefault(k,[]).append(g['killCredit']); sh.setdefault(k,[]).append(g['shells'])
                    if g['first']>=0: first.setdefault(k,[]).append(g['first'])
        prep=[x.get('prepShells',0) for x in n]; adv=[x.get('advance',-1) for x in n]
        print(f"  F{f}: preContactShells {sum(pre)} (per game {mean(pre):.1f}), contact t {mean(ctc):.0f}s, shells before first infantry advance {mean(prep):.1f}/game (advance t {mean(adv):.0f}s), assault-aim<=12 {sum(aon)}/{sum(asl)} ({100*sum(aon)/max(1,sum(asl)):.0f}%), howitzer moves/game {mean(artm):.1f}, gun move-starts/game {mean(mv):.1f}, gun-sec moving {100*mean(ms):.0f}%, fire ratio {mean(fr):.1f}%, observers alive {oal}/{obs} (mean death {mean(od):.0f}s)")
        print("     far>80%: "+", ".join(f"{k.replace('heavy_','')} {100*far[k]/max(1,shots[k]):.0f}% of {shots[k]}" for k in sorted(shots)))
        fa=[];fv=[]
        for a,_ in rows:
            for k,g in a['gun'].items():
                if k.startswith(f+':') and 'far' in g: fa.append(g['far']); fv.append(g['farAvoidable'])
        pc=[];pi=[]
        for a,sec in rows:
            m=re.search(r'보병 첫 사격 전 포병 사격 (\d+), 접촉 전 (\d+)',sec.get(f,''))
            if m: pi.append(int(m.group(1))); pc.append(int(m.group(2)))
        print(f"     direct far shots {sum(fa)}, of which a near target was available {sum(fv)} ({100*sum(fv)/max(1,sum(fa)):.0f}%); obs-tool shots before first infantry fire {sum(pi)}, before contact {sum(pc)}")
        print("     kill credit/game: "+", ".join(f"{k.split(':')[1].replace('heavy_','')} {mean(v):.1f} (shells {mean(sh[k]):.0f}, first {mean(first.get(k,[-1])):.0f}s)" for k,v in sorted(kc.items())))
def sagg(d,label,v,n):
    rows=[art(f'{d}/small_{v}_s{s}.txt') for s in range(1,n+1)]
    rows=[r for r in rows if r]
    wins=sum(1 for a in rows if a['winner']==1)
    LA=[a['startA']-a['endA'] for a in rows]; LB=[a['startB']-a['endB'] for a in rows]
    kc={};sh={};fi={};pre=[]
    sc0=sum(a['start'].get('1:infantry_scout',0) for a in rows); sc1=sum(a['end'].get('1:infantry_scout',0) for a in rows)
    obs=sum(a['n20']['1']['obs'] for a in rows); oal=sum(a['n20']['1']['obsAlive'] for a in rows)
    for a in rows:
        pre.append(a['n20']['1']['preShells'])
        for k,g in a['gun'].items():
            if k.startswith('1:'): kc.setdefault(k,[]).append(g['killCredit']); sh.setdefault(k,[]).append(g['shells']); fi.setdefault(k,[]).append(g['first'])
    gm=[a['n20']['1']['gunMoves'] for a in rows]
    print(f"  {label} {v}: A wins {wins}/{len(rows)}, t {mean([a['t'] for a in rows]):.0f}, A loss {mean(LA):.1f}, B loss {mean(LB):.1f}, preShells/game {mean(pre):.1f}, gunMoves/game {mean(gm):.1f}, scouts alive {sc1}/{sc0}, observers alive {oal}/{obs}; "+", ".join(f"{k.split(':')[1].replace('heavy_','')} kills {mean(v2):.1f} shells {mean(sh[k]):.1f} first {mean(fi[k]):.0f}" for k,v2 in kc.items()))
if __name__=='__main__':
    d=sys.argv[1]; label=sys.argv[2]; n=int(sys.argv[3]) if len(sys.argv)>3 else 10
    cagg(d,label)
    bagg(d,label)
    dagg(d,label)
    print(f'== small {label} (seeds 1-{n})')
    for v in ['hw3s4','fg3','ig3','ctl']:
        if os.path.exists(f'{d}/small_{v}_s1.txt'): sagg(d,label,v,n)
