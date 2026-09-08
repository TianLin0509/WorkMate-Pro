"""Run every WorkMate verification entrypoint against this checkout on Windows."""
from datetime import datetime
import os
from pathlib import Path
import subprocess
import sys
import time
import uuid

ROOT = Path(__file__).resolve().parents[1]


def main():
    if sys.platform != "win32":
        print("WorkMate verification requires Windows and an interactive desktop.", flush=True)
        return 2
    output = ROOT / "artifacts" / (datetime.now().strftime("%Y%m%d-%H%M%S") + "-workmate-checks-" + uuid.uuid4().hex[:8])
    output.mkdir(parents=True, exist_ok=False)
    ps = str(Path(os.environ["SystemRoot"]) / "System32/WindowsPowerShell/v1.0/powershell.exe")
    base = [ps, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File"]
    stages = [
        ("build-and-300-frame-qc", ["scripts/build.ps1"]),
        ("stress-and-18-selftests", ["scripts/verify-v124-stress.ps1", "-OutputDirectory", str(output / "stress")]),
        ("custom-pet-four-step-ui", ["scripts/verify-custom-pet-e2e.ps1", "-OutputDirectory", str(output / "custom-pet-ui")]),
    ]
    started = time.monotonic()
    for name, args in stages:
        tick = time.monotonic()
        print("RUN " + name, flush=True)
        # PowerShell runs each suite in a fresh process so test environment changes
        # cannot leak into the following suite or the caller's application session.
        completed = subprocess.run(base + args, cwd=ROOT)
        elapsed = time.monotonic() - tick
        print(f"CHECK {name} exit={completed.returncode} seconds={elapsed:.2f}", flush=True)
        if completed.returncode != 0:
            print(f"RESULT FAIL stage={name} artifacts={output}", flush=True)
            return 1
    print(f"RESULT PASS stages={len(stages)} seconds={time.monotonic() - started:.2f} artifacts={output}", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
