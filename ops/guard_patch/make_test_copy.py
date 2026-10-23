"""Copies the (read-only) hook files to a scratch folder for testing the patch. Reads Program Files, writes only to the target dir."""
import sys, shutil, os
src = r"C:\Program Files\ClaudeCode\hooks"
dst = sys.argv[1]
os.makedirs(dst, exist_ok=True)
for n in ("kd_guard.ps1", "kd_scope.json"):
    shutil.copyfile(os.path.join(src, n), os.path.join(dst, n))
print("copied to", dst)
