"""
Summarize metadata quality in corpus.jsonl (book_title / author).

Use after parse_dump to catch empty titles before indexing:

    python -m kindle_agent.validate_corpus
    python -m kindle_agent.validate_corpus path/to/corpus.jsonl
"""

from __future__ import annotations

import argparse
import json
import sys
from collections import Counter
from pathlib import Path


def main() -> None:
    ap = argparse.ArgumentParser(description="Report book_title / author coverage in corpus.jsonl")
    ap.add_argument(
        "corpus",
        type=Path,
        nargs="?",
        default=Path(__file__).resolve().parents[1] / "data" / "corpus.jsonl",
    )
    ap.add_argument(
        "--sample",
        type=int,
        default=8,
        help="Max sample rows to print per problem bucket (default 8)",
    )
    args = ap.parse_args()
    if not args.corpus.is_file():
        print(f"Missing file: {args.corpus}", file=sys.stderr)
        raise SystemExit(2)

    rows: list[dict] = []
    with args.corpus.open(encoding="utf-8") as f:
        for line_no, line in enumerate(f, start=1):
            line = line.strip()
            if not line:
                continue
            try:
                rows.append(json.loads(line))
            except json.JSONDecodeError as e:
                print(f"Line {line_no}: JSON error: {e}", file=sys.stderr)
                raise SystemExit(1) from e

    n = len(rows)
    if n == 0:
        print("Corpus is empty.")
        raise SystemExit(1)

    def title_of(r: dict) -> str:
        return (r.get("book_title") or "").strip()

    def author_of(r: dict) -> str:
        return (r.get("author") or "").strip()

    empty_title = [r for r in rows if not title_of(r)]
    empty_author = [r for r in rows if not author_of(r)]
    both = [r for r in rows if not title_of(r) and not author_of(r)]

    title_key = Counter(title_of(r) for r in rows if title_of(r))

    print(f"File: {args.corpus}")
    print(f"Rows: {n}")
    print(f"Distinct non-empty book_title: {len(title_key)}")
    print(f"Rows with empty book_title: {len(empty_title)}")
    print(f"Rows with empty author: {len(empty_author)}")
    print(f"Rows with both empty: {len(both)}")

    if both:
        print("\n--- Sample: both book_title and author empty ---")
        for r in both[: args.sample]:
            print(f"  id={r.get('id')} location={r.get('location')} file={r.get('source_file')}")

    if empty_title and not both:
        print("\n--- Sample: empty book_title (author may still be set) ---")
        for r in empty_title[: args.sample]:
            a = author_of(r)[:60]
            print(f"  id={r.get('id')} author_preview={a!r}...")

    if title_key:
        print("\nTop book_title values (count):")
        for t, c in title_key.most_common(10):
            print(f"  {c:5d}  {t[:80]}{'…' if len(t) > 80 else ''}")

    if len(empty_title) == 0:
        print("\nOK: every row has a non-empty book_title.")
        raise SystemExit(0)
    print(
        "\nHint: fix parse_dump heuristics, or edit spikes and re-run parse_dump; "
        "for one-offs, edit JSONL lines in an editor (one JSON object per line).",
        file=sys.stderr,
    )
    raise SystemExit(1)


if __name__ == "__main__":
    main()
