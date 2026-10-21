import json,re,sys,statistics as st
D=sys.argv[1]; SM=int(sys.argv[2])
vs=['base','noart','noartK','noartF','noKig','noKfg','noKhow']
GUN={'heavy_infantry_gun','heavy_field_gun','heavy_howitzer'}
def load(v,s):
    t=open(f'{D}/c_{v}_s{s}.txt',encoding='utf-8').read()
    j=json.loads(re.search(r'ARTYFX (.*)',t).group(1))
    m=re.search(r'# \S+ seed \d+: (\w+) at ([\d.]+)s',t)
    j['res']=m.group(1); j['dur']=float(m.group(2)); return j
def lost(j,side,gun=False):
    return sum(c-j['end'].get(k,0) for k,c in j['start'].items() if int(k.split(':')[0])==side and ((k.split(':')[1] in GUN)==gun))
for label,seeds in (('seeds 1-3',range(1,4)),(f'seeds 1-{SM}',range(1,SM+1))):
    print('##',label)
    print('| 구성 | 한국 승 | 평균 초 | 한국 보병계 손실 | 한국 포 손실 | 봉천 보병계 손실 | 봉천 포 손실 | 봉천/한국 손실 | 한국 생존 |')
    print('|---|---|---|---|---|---|---|---|---|')
    for v in vs:
        R=[load(v,s) for s in seeds]
        w=sum(1 for j in R if j['winner']==1)
        la=st.mean(lost(j,1) for j in R); lb=st.mean(lost(j,2) for j in R)
        print(f"| {v} | {w}/{len(R)} | {st.mean(j['dur'] for j in R):.0f} | {la:.0f} | {st.mean(lost(j,1,True) for j in R):.1f} | {lb:.0f} | {st.mean(lost(j,2,True) for j in R):.1f} | {lb/la:.2f} | {st.mean(j['endA'] for j in R):.0f} |")
print('## 포 종류별 (base, seeds 1-%d, 판 평균)'%SM)
print('| 세력:포 | 문 | 포탄/판 | 착탄 명중% | 반경 안 적/탄 | 평균 거리 | 첫 사격 초 | 살상 크레딧/판 | 크레딧/포탄 | 제압량(추정)/판 | 포 생존% |')
print('|---|---|---|---|---|---|---|---|---|---|---|')
SUP={'heavy_infantry_gun':150,'heavy_field_gun':250,'heavy_howitzer':400}
R=[load('base',s) for s in range(1,SM+1)]
for k in sorted({k for j in R for k in j['gun']}):
    g=[j['gun'].get(k,{}) for j in R]; sh=sum(x.get('shells',0) for x in g); n=len(R)
    ds=sum(x.get('meanDist',0)*x.get('shells',0) for x in g)
    fs=[x['first'] for x in g if x and x['first']>=0]
    u0=sum(j['start'].get(k,0) for j in R); u1=sum(j['end'].get(k,0) for j in R)
    kc=sum(x.get('killCredit',0) for x in g)
    sup=sum(x.get('suppRes',0) for x in g)*SUP[k.split(':')[1]]
    print(f"| {k} | {u0//n} | {sh/n:.0f} | {100*sum(x.get('hit',0) for x in g)/max(sh,1):.0f} | {sum(x.get('inSplash',0) for x in g)/max(sh,1):.2f} | {ds/max(sh,1):.0f} | {st.mean(fs) if fs else -1:.0f} | {kc/n:.0f} | {kc/max(sh,1):.2f} | {sup/n:.0f} | {100*u1/max(u0,1):.0f} |")
print('## 거리 분포 % (0-20/20-40/40-60/60-80/80+)')
for k in sorted({k for j in R for k in j['gun']}):
    b=[sum(j['gun'].get(k,{}).get('bucket',[0]*5)[i] for j in R) for i in range(5)]; T=max(sum(b),1)
    print(f"- {k}: "+' / '.join(f'{100*x/T:.0f}' for x in b))
print('## 쌍대 차이 (구성 - base, 같은 시드; 평균 [표준오차])')
def se(x): return st.stdev(x)/len(x)**.5 if len(x)>1 else 0
for v in ['noart','noartK','noartF','noKig','noKfg','noKhow']:
    a=[];b=[];c=[];d=[]
    for s in range(1,SM+1):
        x=load(v,s); y=load('base',s)
        a.append(lost(x,1)-lost(y,1)); b.append(lost(x,2)-lost(y,2)); c.append(x['dur']-y['dur']); d.append(x['endA']-y['endA'])
    print(f"- {v} - base: 한국손실 {st.mean(a):+.0f} [{se(a):.0f}], 봉천손실 {st.mean(b):+.0f} [{se(b):.0f}], 소요 {st.mean(c):+.0f}s [{se(c):.0f}], 한국생존 {st.mean(d):+.0f} [{se(d):.0f}]")
print('## 시드별')
for s in range(1,SM+1):
    print(f"- s{s}: "+' | '.join(f"{v} {load(v,s)['res']}/{load(v,s)['dur']:.0f}s K-{lost(load(v,s),1)} F-{lost(load(v,s),2)}" for v in vs))
