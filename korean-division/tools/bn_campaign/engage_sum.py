# engage (2026-10-19) 요약: python tools/bn_campaign/engage_sum.py base after s_ENG70 ... (docs/engage_raw/<폴더>)
import gzip, json, glob, os, sys, statistics as st
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "docs", "engage_raw")
def load(p):
    return json.load(gzip.open(p) if p.endswith('.gz') else open(p, encoding='utf8'))
def summ(files, defside='2'):
    rb=[0]*9; rh=[0]*9; cov=[0,0,0]; uf=0; covA=0; ufA=0; w24=[]; wins=0; ke=[]; dur=[]; fs=[]; kd=[]; n=0; tl=[]
    for f in files:
        d=load(f); n+=1; c=d['counters']
        for s in '12':
            for b in range(9):
                rb[b]+=c.get(f'{s}|rb.rifle.{b}',0); rh[b]+=c.get(f'{s}|rbh.rifle.{b}',0)
        cols=d['rowCols']; I={k:i for i,k in enumerate(cols)}
        for r in d['rows']:
            u=r[I['underFire']]
            if not u: continue
            if str(r[I['side']])==defside:
                uf+=u; cov[0]+=r[I['cTrench']]; cov[1]+=r[I['cForest']]; cov[2]+=r[I['cOther']]
            else:
                ufA+=u; covA+=r[I['cTrench']]+r[I['cForest']]+r[I['cOther']]
        sn=c.get(defside+'|sup.n',0)
        if sn: w24.append(c.get(defside+'|sup.within24',0)/sn)
        atk='1' if defside=='2' else '2'
        la=d['start'][atk]-d['alive'][atk]; ld=d['start'][defside]-d['alive'][defside]
        wins+= d['result']=='won'; ke.append(ld/max(1,la)); dur.append(d['dur']); fs.append(d['firstShot'] or 0)
        kd.append((c.get(defside+'|kills.total',0), c.get(atk+'|kills.total',0)))
        tl.append(la/d['start'][atk])
    S=sum(rb) or 1
    mean=sum((b*3+1.5)*rb[b] for b in range(9))/S
    hit=sum(rh)/S
    dk=sum(a for a,b in kd); ak=sum(b for a,b in kd)
    return dict(n=n, mean=round(mean,1), p12_18=round(100*(rb[4]+rb[5])/S), p9_21=round(100*(rb[3]+rb[4]+rb[5]+rb[6])/S), p21=round(100*(rb[7]+rb[8])/S), hit=round(100*hit,1),
                defCover=round(100*sum(cov)/max(1,uf)), defTrench=round(100*cov[0]/max(1,uf)), atkCover=round(100*covA/max(1,ufA)),
                w24=round(100*st.mean(w24)) if w24 else None, win=f"{wins}/{n}", KE=round(st.mean(ke),2), atkLoss=round(100*st.mean(tl)), dur=round(st.mean(dur)), first=round(st.mean(fs),1),
                defKills_per_atkKill=round(dk/max(1,ak),2))
def run(name):
    base=os.path.join(ROOT,name)
    out={}
    for g,pat in (('T2','T_r2.0_RM_s*.json.gz'),('T3','T_r3.0_RM_s*.json.gz'),('M','M_open_RM_s*.json.gz')):
        fl=sorted(glob.glob(os.path.join(base,'runs',pat)))
        if fl: out[g]=summ(fl,'2')
    for k in 'ABC':
        fl=sorted(glob.glob(os.path.join(base,'abc',k+'*.json*')))
        if fl: out[k]=summ(fl,'2')
    return out
if __name__=='__main__':
    for name in sys.argv[1:]:
        r=run(name)
        print('==',name)
        for g,v in r.items(): print(' ',g,v)
