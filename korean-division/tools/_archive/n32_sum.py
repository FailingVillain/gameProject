import re,sys,glob,collections
# tools/n32_sum.py <raw dir> : variant x set -> wins/5, K/E (우리 손실 / 적 손실), timeouts, N32 시작/성공
d=sys.argv[1] if len(sys.argv)>1 else 'docs/n32_raw'
res=collections.defaultdict(list)
for f in sorted(glob.glob(d+'/*_[A-Z][0-9].txt')):
    m=re.match(r'.*[/\\](\w+?)_([A-Z])(\d)\.txt',f)
    if not m: continue
    v,k,sd=m.groups()
    t=open(f,encoding='utf-8',errors='replace').read()
    st=re.search(r'seed \d+: (\w+) at',t)
    l=re.findall(r'lost (\d+), alive',t)
    ta=re.search(r'\[N32 참호돌격\] 시작 (\d+) \(AI (\d+)\) 성공 (\d+) 실패 (\d+) 취소 (\d+)',t)
    if not st or len(l)<2: continue
    res[(v,k)].append((st.group(1),int(l[0]),int(l[1]),ta.groups() if ta else None))
for (v,k),rows in sorted(res.items(),key=lambda x:(x[0][1],x[0][0])):
    w=sum(1 for r in rows if r[0]=='won'); to=sum(1 for r in rows if r[0]=='running')
    me=sum(r[1] for r in rows); en=sum(r[2] for r in rows)
    tas=[sum(int(r[3][i]) for r in rows if r[3]) for i in (0,2,3,4)]
    print(f"{k} {v:5s} wins {w}/{len(rows)} K/E {me/max(1,en):.2f} (own lost {me/len(rows):.0f}, enemy lost {en/len(rows):.0f}) timeouts {to} | TA start {tas[0]} done {tas[1]} fail {tas[2]} cancel {tas[3]}")
