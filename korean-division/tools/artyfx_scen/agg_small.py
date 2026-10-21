import json,glob,re,sys,statistics as st
D=sys.argv[1]; SM=int(sys.argv[2]) if len(sys.argv)>2 else 20
vs=['ctl','scout4','ig3','ig3eq','fg3','fg3eq','hw3s4','hw3s4eq','mix','mixeq']
GUN={'heavy_infantry_gun','heavy_field_gun','heavy_howitzer'}
def load(v,s):
    t=open(f'{D}/small_{v}_s{s}.txt',encoding='utf-8').read()
    j=json.loads(re.search(r'ARTYFX (.*)',t).group(1)); return j
def lost(j,side,gun=False):
    n=0
    for k,c in j['start'].items():
        o,key=k.split(':')
        if int(o)==side and ((key in GUN)==gun): n+=c-j['end'].get(k,0)
    return n
def row(v,seeds):
    R=[load(v,s) for s in seeds]
    w=sum(1 for j in R if j['winner']==1); dr=sum(1 for j in R if j['winner']==0)
    t=st.mean(j['t'] for j in R)
    la=st.mean(lost(j,1) for j in R); lb=st.mean(lost(j,2) for j in R)
    lag=st.mean(lost(j,1,True) for j in R)
    sa=st.mean(j['endA'] for j in R if True); 
    return dict(n=len(R),win=w,draw=dr,t=t,la=la,lb=lb,lag=lag,ratio=lb/max(la,.01),R=R)
for label,seeds in (('seeds 1-5',range(1,6)),(f'seeds 1-{SM}',range(1,SM+1))):
    print('##',label)
    print('| 구성 | A 승 | 평균 초 | A 보병계 손실 | A 포 손실 | B 손실 | B손실/A손실 | A 생존(끝) |')
    print('|---|---|---|---|---|---|---|---|')
    for v in vs:
        r=row(v,seeds); sv=st.mean(j['endA'] for j in r['R'])
        print(f"| {v} | {r['win']}/{r['n']} | {r['t']:.0f} | {r['la']:.1f} | {r['lag']:.1f} | {r['lb']:.1f} | {r['ratio']:.2f} | {sv:.1f} |")
print('## 포 종류별 (seeds 1-%d 합산, 포 보유 구성)'%SM)
print('| 구성 | 포 | 포탄/판 | 착탄 명중% | 반경 안 적/탄 | 평균 거리 | 첫 사격 초 | 살상 크레딧/판 | 포 생존% |')
print('|---|---|---|---|---|---|---|---|---|')
for v in ['ig3','fg3','hw3s4','mix']:
    R=[load(v,s) for s in range(1,SM+1)]
    keys=sorted({k for j in R for k in j['gun'] if k.startswith('1:')})
    for k in keys:
        sh=sum(j['gun'].get(k,{}).get('shells',0) for j in R); hit=sum(j['gun'].get(k,{}).get('hit',0) for j in R)
        ins=sum(j['gun'].get(k,{}).get('inSplash',0) for j in R); ds=sum(j['gun'].get(k,{}).get('meanDist',0)*j['gun'].get(k,{}).get('shells',0) for j in R)
        kc=sum(j['gun'].get(k,{}).get('killCredit',0) for j in R)
        fs=[j['gun'][k]['first'] for j in R if k in j['gun'] and j['gun'][k]['first']>=0]
        u0=sum(j['start'].get(k,0) for j in R); u1=sum(j['end'].get(k,0) for j in R)
        print(f"| {v} | {k[2:]} | {sh/len(R):.1f} | {100*hit/max(sh,1):.0f} | {ins/max(sh,1):.2f} | {ds/max(sh,1):.0f} | {st.mean(fs) if fs else -1:.0f} | {kc/len(R):.2f} | {100*u1/max(u0,1):.0f} |")
print('## 거리 분포 (포탄 비율%, 0-20/20-40/40-60/60-80/80+)')
for v in ['ig3','fg3','hw3s4','mix']:
    R=[load(v,s) for s in range(1,SM+1)]
    keys=sorted({k for j in R for k in j['gun'] if k.startswith('1:')})
    for k in keys:
        b=[sum(j['gun'].get(k,{}).get('bucket',[0]*5)[i] for j in R) for i in range(5)]; T=max(sum(b),1)
        print(f"- {v} {k[2:]}: "+' / '.join(f'{100*x/T:.0f}' for x in b))
print('## 쌍대 (같은 시드) 차이: 구성 - 대조(ctl), 평균 [표준오차]; 생존 차이 = A 끝 생존자 수')
def se(x): return st.stdev(x)/len(x)**.5 if len(x)>1 else 0
base={s:load('ctl',s) for s in range(1,SM+1)}
for v,ref in (('scout4','ctl'),('ig3','ctl'),('fg3','ctl'),('hw3s4','ctl'),('mix','ctl'),('ig3','ig3eq'),('fg3','fg3eq'),('hw3s4','hw3s4eq'),('mix','mixeq')):
    d_b=[];d_a=[];d_s=[];d_w=[]
    for s in range(1,SM+1):
        x=load(v,s); y=load(ref,s)
        d_b.append(lost(x,2)-lost(y,2)); d_a.append(lost(x,1)-lost(y,1)); d_s.append(x['endA']-y['endA']); d_w.append((x['winner']==1)-(y['winner']==1))
    print(f"- {v} - {ref}: B손실 {st.mean(d_b):+.1f} [{se(d_b):.1f}], A보병손실 {st.mean(d_a):+.1f} [{se(d_a):.1f}], A생존 {st.mean(d_s):+.1f} [{se(d_s):.1f}], 승수 {sum(d_w):+d}/{SM}")
