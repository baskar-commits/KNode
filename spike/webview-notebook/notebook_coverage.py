"""
Compare Kindle Notebook library titles (from a deduped extract) to books present in corpus.jsonl.

Use this to see which books still need a WebView capture (detail view with highlights).

Run from this folder (venv):  python notebook_coverage.py

Exits 1 if any library book is missing from the corpus (optional CI hook).
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path


LIBRARY_MARK = "ANNOTATED BOOKS FROM YOUR LIBRARY"
SECTION_NOTES = "YOUR KINDLE NOTES FOR:"
CAPTURE_LINE = "\n" + ("=" * 80)


def _norm(s: str) -> str:
    return " ".join(
        s.replace("\u00a0", " ")
        .replace("\u202f", " ")
        .strip()
        .split()
    ).lower()


def parse_annotated_library_chunks(raw: str) -> list[list[tuple[str, str]]]:
    """Each inner list is one capture's (title, author) pairs from the library index."""
    captures: list[list[tuple[str, str]]] = []
    pos = 0
    while True:
        i = raw.find(LIBRARY_MARK, pos)
        if i < 0:
            break
        j = raw.find(SECTION_NOTES, i)
        k = raw.find(CAPTURE_LINE, i)
        end_candidates = [x for x in (j, k) if x > i]
        end = min(end_candidates) if end_candidates else len(raw)
        chunk = raw[i + len(LIBRARY_MARK) : end]
        captures.append(_parse_library_chunk(chunk))
        pos = i + 1
    return captures


def _parse_library_chunk(chunk: str) -> list[tuple[str, str]]:
    lines = [ln.replace("\u00a0", " ").replace("\u202f", " ") for ln in chunk.splitlines()]
    i = 0
    books: list[tuple[str, str]] = []
    while i < len(lines):
        while i < len(lines) and not lines[i].strip():
            i += 1
        if i >= len(lines):
            break
        if lines[i].strip().startswith("(") and "recently" in lines[i].lower():
            i += 1
            continue
        title_parts: list[str] = []
        while i < len(lines) and lines[i].strip():
            s = lines[i].strip()
            if s.startswith("By:"):
                break
            title_parts.append(s)
            i += 1
        title = " ".join(title_parts).strip()
        author = ""
        while i < len(lines) and not lines[i].strip():
            i += 1
        if i < len(lines) and lines[i].strip().startswith("By:"):
            author = lines[i].strip()[3:].strip()
            i += 1
        if title:
            books.append((title, author))
    return books


def corpus_titles_and_authors(corpus: Path) -> tuple[set[str], set[tuple[str, str]]]:
    titles: set[str] = set()
    pairs: set[tuple[str, str]] = set()
    with corpus.open(encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            r = json.loads(line)
            t = _norm(r.get("book_title") or "")
            a = _norm(r.get("author") or "")
            if t:
                titles.add(t)
            if t and a:
                pairs.add((t, a))
    return titles, pairs


def main() -> None:
    ap = argparse.ArgumentParser(description="Library vs corpus coverage")
    ap.add_argument(
        "corpus",
        type=Path,
        nargs="?",
        default=Path(__file__).resolve().parents[2] / "mvp" / "data" / "corpus.jsonl",
    )
    ap.add_argument(
        "deduped",
        type=Path,
        nargs="?",
        default=Path(__file__).resolve().parent / "spike_extract_deduped.txt",
    )
    ap.add_argument(
        "--strict",
        action="store_true",
        help="Match title+author pairs (stricter than title-only)",
    )
    args = ap.parse_args()

    if not args.corpus.is_file():
        print(f"Missing corpus: {args.corpus}", file=sys.stderr)
        raise SystemExit(2)
    if not args.deduped.is_file():
        print(f"Missing deduped extract: {args.deduped}", file=sys.stderr)
        print("Run dedupe_spike_extract.py after capturing.", file=sys.stderr)
        raise SystemExit(2)

    raw = args.deduped.read_text(encoding="utf-8", errors="replace")
    capture_lists = parse_annotated_library_chunks(raw)
    if not capture_lists:
        print("No ANNOTATED BOOKS FROM YOUR LIBRARY sections found in extract.", file=sys.stderr)
        raise SystemExit(1)

    library_titles: set[str] = set()
    library_pairs: set[tuple[str, str]] = set()
    for lst in capture_lists:
        for title, author in lst:
            library_titles.add(_norm(title))
            if author:
                library_pairs.add((_norm(title), _norm(author)))

    corpus_title_set, corpus_pair_set = corpus_titles_and_authors(args.corpus)

    if args.strict:
        missing_items = sorted(library_pairs - corpus_pair_set)
        label = "pair(s)"
        lines_out = [f"{t}  |  {a}" for t, a in missing_items]
    else:
        missing_items = sorted(library_titles - corpus_title_set)
        label = "title(s)"
        lines_out = list(missing_items)

    print(f"Corpus: {args.corpus}")
    print(f"Extract: {args.deduped}")
    print(f"Library index sections found: {len(capture_lists)}")
    print(f"Distinct titles in library list (union): {len(library_titles)}")
    print(f"Distinct titles in corpus: {len(corpus_title_set)}")
    print()

    if not missing_items:
        print("OK: every library title appears in corpus (normalized title match).")
        raise SystemExit(0)

    print(f"Capture these in WebView (missing ~{len(missing_items)} {label}):")
    print("-" * 72)
    for line in lines_out:
        print(f"  • {line}")
    print("-" * 72)
    print(
        "For each: open the book, wait for highlights, press Enter in the terminal.",
        file=sys.stderr,
    )
    raise SystemExit(1)


if __name__ == "__main__":
    main()
