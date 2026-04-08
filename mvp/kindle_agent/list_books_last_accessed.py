"""Print book title, author, and Last accessed date from spike_extract_deduped.txt (Notebook export)."""

from __future__ import annotations

import argparse
import json
import re
from collections import defaultdict
from datetime import datetime
from pathlib import Path

SECTION = re.compile(r"^YOUR KINDLE NOTES FOR:\s*$", re.MULTILINE | re.I)
LAST = re.compile(r"^last\s+accessed\s+on\s*(.+)$", re.I)
STATS = re.compile(
    r"^[\d\u202f\s]+Highlights?\s*\|\s*[\d\u202f\s]+Notes?\s*$",
    re.I,
)


def _norm_line(s: str) -> str:
    return s.replace("\u00a0", " ").replace("\u202f", " ").strip()


def _next_non_empty(lines: list[str], start: int) -> int:
    i = start
    while i < len(lines) and not _norm_line(lines[i]):
        i += 1
    return i


def parse_sections(deduped: Path) -> dict[tuple[str, str], list[datetime]]:
    raw = deduped.read_text(encoding="utf-8", errors="replace")
    parts = SECTION.split(raw)
    per_book: dict[tuple[str, str], list[datetime]] = defaultdict(list)

    for part in parts[1:]:
        lines = part.splitlines()
        ti = _next_non_empty(lines, 0)
        if ti >= len(lines):
            continue
        title = _norm_line(lines[ti])
        ai = _next_non_empty(lines, ti + 1)
        author = ""
        if ai < len(lines):
            cand = _norm_line(lines[ai])
            if not cand.lower().startswith("last accessed") and not STATS.match(cand):
                author = cand
        accessed: datetime | None = None
        for ln in lines:
            m = LAST.match(_norm_line(ln))
            if m:
                rest = m.group(1).strip()
                try:
                    accessed = datetime.strptime(rest, "%A, %B %d, %Y")
                except ValueError:
                    accessed = None
                break
        if title and accessed:
            per_book[(title, author)].append(accessed)

    return per_book


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument(
        "--deduped",
        type=Path,
        default=Path(__file__).resolve().parents[2]
        / "spike"
        / "webview-notebook"
        / "spike_extract_deduped.txt",
    )
    ap.add_argument(
        "--corpus",
        type=Path,
        default=Path(__file__).resolve().parents[1] / "data" / "corpus.jsonl",
    )
    args = ap.parse_args()

    per_book = parse_sections(args.deduped)
    rows: list[tuple[datetime, str, str, int]] = []
    for (title, author), dts in per_book.items():
        last = max(dts)
        rows.append((last, title, author, len(dts)))

    rows.sort(key=lambda x: (-x[0].timestamp(), x[1].lower()))

    print(f"Source extract: {args.deduped}")
    print(f"Distinct books (YOUR KINDLE NOTES blocks): {len(rows)}")
    print()
    print(f"{'Last accessed':26} | {'Book title':55} | Author")
    print("-" * 135)
    for last, title, author, n in rows:
        d = last.strftime("%Y-%m-%d") + " (" + last.strftime("%a") + ")"
        auth = author or "(no author line)"
        print(f"{d:26} | {title[:55]:55} | {auth}")
        if len(title) > 55:
            print(f"{'':26} | {title[55:]:55} |")

    if args.corpus.is_file():
        c_pairs: set[tuple[str, str]] = set()
        with args.corpus.open(encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if not line:
                    continue
                r = json.loads(line)
                c_pairs.add(
                    (r.get("book_title", "").strip(), (r.get("author") or "").strip())
                )
        missing = c_pairs - set(per_book.keys())
        if missing:
            print()
            print(f"In corpus but no Last accessed in extract: {len(missing)}")
            for t, a in sorted(missing, key=lambda x: x[0].lower())[:20]:
                print(f"  - {t} | {a}")


if __name__ == "__main__":
    main()
