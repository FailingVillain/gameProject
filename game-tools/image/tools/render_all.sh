#!/bin/bash
# usage: tools/render_all.sh <logfile> <unit>...   renders units sequentially (make_unit.py) and logs border / collide lines
cd "$(dirname "$0")/.."
log=$1; shift
for n in "$@"; do
  echo "=== $n $(date +%T)" >> $log
  if grep -q '"kind": "vehicle"' units/$n.json; then python make_vehicle.py $n >> $log 2>&1; else python make_unit.py $n >> $log 2>&1; fi
  echo "--- exit $?" >> $log
done
echo ALLDONE >> $log
