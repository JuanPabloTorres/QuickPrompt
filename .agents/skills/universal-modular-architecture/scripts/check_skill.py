#!/usr/bin/env python3
"""Dependency-free basic structural check; not a substitute for skills-ref validation."""
from pathlib import Path
import re
import sys


def check_skill(root):
    root = root.resolve()
    s = (root / 'SKILL.md').read_text(encoding='utf-8')
    if not s.startswith('---\n') or '\n---' not in s[4:]:
        raise ValueError('Missing frontmatter delimiters')
    front = s.split('---', 2)[1]
    name = re.search(r'^name: (.+)$', front, re.M)
    desc = re.search(r'^description: (.+)$', front, re.M)
    if not name or not desc:
        raise ValueError('Missing name or description')
    if name.group(1) != root.name or not re.fullmatch(r'[a-z0-9]+(?:-[a-z0-9]+)*', name.group(1)):
        raise ValueError('Invalid skill name or directory mismatch')
    if len(name.group(1)) > 64 or len(desc.group(1)) > 1024:
        raise ValueError('Name or description too long')
    targets = re.findall(r'`((?:references|templates)/[^`]+\.md)`', s)
    targets += re.findall(r'\]\(((?:references|templates)/[^)]+\.md)(?:#[^)]*)?\)', s)
    for target in set(targets):
        resolved = (root / target).resolve()
        if not resolved.is_relative_to(root) or not resolved.is_file():
            raise ValueError(f'Missing or nonlocal resource: {target}')
    return len(set(targets))


def main():
    roots = [Path(arg) for arg in sys.argv[1:]] or [Path(__file__).resolve().parents[1]]
    for root in roots:
        count = check_skill(root)
        print(f'PASS basic structure and {count} local references: {root.name}')
    print('Scope: structural checks only; no full YAML or behavioral qualification.')


if __name__ == '__main__':
    try:
        main()
    except (OSError, ValueError) as error:
        print(f'FAIL: {error}', file=sys.stderr)
        sys.exit(1)
