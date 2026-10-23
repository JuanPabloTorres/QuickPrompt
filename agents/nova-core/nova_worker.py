"""Launcher only. Generic inference belongs to Adaptive Core, not this app."""
import argparse
from pathlib import Path
import sys

if __name__ == '__main__':
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument('--sdk-root', required=True)
    args, remaining = parser.parse_known_args()
    sys.path.insert(0, str(Path(args.sdk_root).resolve()))
    sys.argv = [sys.argv[0], *remaining]
    from adaptive_core.host import main
    raise SystemExit(main())
