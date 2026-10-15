#!/usr/bin/env python3
"""Runs the tool tests.

`python3 tools/tests/run.py` is the short form of
`python3 -m unittest discover -s tools/tests -v`; both work, and both need nothing but Python 3.
The `-v` matters: the test names say what each one prevents, and a silent pass hides that.
"""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent


def main() -> int:
    # The test modules import `modules`, which lives beside them and is not a package, so the
    # directory has to be importable before discovery starts.
    sys.path.insert(0, str(HERE))

    suite = unittest.defaultTestLoader.discover(start_dir=str(HERE), pattern="test_*.py")
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    return 0 if result.wasSuccessful() else 1


if __name__ == "__main__":
    sys.exit(main())
