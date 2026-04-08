#!/usr/bin/env python3
"""
Convert selected docs/*.md to standalone HTML in docs/ (same folder as the sources).

Usage (from repo root):
    python -m pip install -r scripts/requirements-doc-html.txt
    python scripts/build_doc_html.py

GitHub repo paths in generated links default to baskar-commits/KNode; override with env GITHUB_DOCS_SLUG=owner/repo
"""

from __future__ import annotations

import os
import re
import sys
from pathlib import Path

try:
    import markdown
except ImportError:
    print("Install dependencies: python -m pip install -r scripts/requirements-doc-html.txt", file=sys.stderr)
    sys.exit(1)

ROOT = Path(__file__).resolve().parent.parent
DOCS = ROOT / "docs"
ASSETS = DOCS / "assets"

SLUG = os.environ.get("GITHUB_DOCS_SLUG", "baskar-commits/KNode").strip("/")
GITHUB_BLOB = f"https://github.com/{SLUG}/blob/main"
GITHUB_TREE = f"https://github.com/{SLUG}/tree/main"

# Basenames (without .md) that get a local .html sibling on Pages
GENERATED_STEMS = frozenset(
    {"KNODE-MVP-GUIDE", "KNODE-INSTALL", "KNODE-FIRST-RUN", "KNODE-ARCHITECTURE"}
)

SOURCES = [
    "KNODE-MVP-GUIDE.md",
    "KNODE-INSTALL.md",
    "KNODE-FIRST-RUN.md",
    "KNODE-ARCHITECTURE.md",
]


def title_from_markdown(text: str) -> str:
    for line in text.splitlines():
        line = line.strip()
        if line.startswith("# "):
            return line[2:].strip()
    return "Documentation"


def fix_href(href: str) -> str:
    if not href or href.startswith("http://") or href.startswith("https://") or href.startswith("mailto:"):
        return href
    if href.startswith("#"):
        return href
    base, sep, frag = href.partition("#")
    frag_suffix = f"#{frag}" if sep else ""
    if base.startswith("../"):
        path = base[3:]
        if path.endswith(".md"):
            return f"{GITHUB_BLOB}/{path}{frag_suffix}"
        return f"{GITHUB_TREE}/{path}{frag_suffix}"
    if base.endswith(".md"):
        stem = base[:-3]
        if stem in GENERATED_STEMS:
            return f"{stem}.html{frag_suffix}"
        return f"{GITHUB_BLOB}/docs/{base}{frag_suffix}"
    return href + frag_suffix


def rewrite_links(html: str) -> str:
    def sub(m: re.Match) -> str:
        attr, quote, href, q2 = m.group(1), m.group(2), m.group(3), m.group(4)
        if attr.lower() != "href":
            return m.group(0)
        return f'{attr}={quote}{fix_href(href)}{q2}'

    return re.sub(r'(href)=(["\'])([^"\']+)(\2)', sub, html, flags=re.I)


def wrap_html(title: str, body: str) -> str:
    return f"""<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>{title} | Knode docs</title>
  <link rel="stylesheet" href="assets/docs-html.css" />
</head>
<body>
  <article class="doc">
    <a class="back" href="index.html">← Documentation hub</a>
    {body}
  </article>
</body>
</html>
"""


def main() -> None:
    md = markdown.Markdown(
        extensions=["tables", "fenced_code", "nl2br", "sane_lists"],
        extension_configs={},
    )
    for name in SOURCES:
        src = DOCS / name
        if not src.exists():
            print(f"Skip (missing): {src}", file=sys.stderr)
            continue
        text = src.read_text(encoding="utf-8")
        title = title_from_markdown(text)
        md.reset()
        body = md.convert(text)
        body = rewrite_links(body)
        out = DOCS / (name[:-3] + ".html")
        out.write_text(wrap_html(title, body), encoding="utf-8")
        print(f"Wrote {out.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
