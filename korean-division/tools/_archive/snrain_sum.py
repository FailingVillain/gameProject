import re,sys,collections
d=collections.defaultdict(list)
for l in sys.stdin:
    m=re.match(r'(\S+) (\w+?)(?:_rain)? seed (\d+): (won|lost) at ([\d.]+)s .*?lost (\d+), alive \d+ lost (\d+)',l)
    if m: d[(m[1],m[2])].append((m[4]=='won',float(m[5]),int(m[6]),int(m[7])))
for k,v in d.items():
    n=len(v);print(k,n,'win',sum(x[0] for x in v),'t',round(sum(x[1] for x in v)/n),'K',round(sum(x[2] for x in v)/n),'E',round(sum(x[3] for x in v)/n))
