cd "$(dirname "$0")/.."
for f in units/*.json; do n=$(basename $f .json); case $n in tank-*|supply-wagon|badge-*) continue;; esac
 echo "== $n"; timeout 900 python tools/collide.py $n 2>&1 | tail -6; done
