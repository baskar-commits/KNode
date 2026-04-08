"""
Parse deduped spike_extract*.txt into structured highlight records (JSONL).

Looks for sections introduced by YOUR KINDLE NOTES FOR:, then title, author,
Last accessed line, "N Highlights | M Notes", then repeating:
  <Color> highlight | Location: <n>
  Options
  <text...>
  Note: ... (optional, applies to preceding highlight)
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from datetime import datetime
from pathlib import Path

SECTION_MARK = re.compile(r"^YOUR KINDLE NOTES FOR:\s*$", re.IGNORECASE | re.MULTILINE)
# Kindle uses narrow no-break space (U+202F) before "Highlights" in some dumps;
# some sections use singular "1 Highlight | 0 Notes".
STATS_LINE = re.compile(
    r"^[\d\u202f\s]+Highlights?\s*\|\s*[\d\u202f\s]+Notes?\s*$",
    re.IGNORECASE,
)
# Print books use "Page:"; reflowable books often use "Location:".
HIGHLIGHT_HEAD = re.compile(
    r"^(?P<color>\w+)\s+highlight\s*\|\s*(?:Location|Page):\s*(?P<loc>[\d,]+)\s*$",
    re.IGNORECASE,
)
# Notebook often inserts a blank line immediately after "YOUR KINDLE NOTES FOR:".
LAST_ACCESSED_PREFIX = re.compile(r"^last\s+accessed", re.IGNORECASE)
LAST_ACCESSED_LINE = re.compile(
    r"^last\s+accessed\s+on\s*(.+)$",
    re.IGNORECASE,
)


def _norm_line(line: str) -> str:
    return line.replace("\u00a0", " ").replace("\u202f", " ").strip()


def _stable_id(book: str, location: str, text: str) -> str:
    h = hashlib.sha256(
        f"{book}|{location}|{text[:500]}".encode("utf-8", errors="replace")
    ).hexdigest()[:16]
    return f"hl_{h}"


def _next_non_empty_index(lines: list[str], start: int) -> int:
    i = start
    while i < len(lines) and not _norm_line(lines[i]):
        i += 1
    return i


def parse_section(lines: list[str], source_file: str, section_idx: int) -> list[dict]:
    """Parse one YOUR KINDLE NOTES FOR block (lines do not include the marker line)."""
    records: list[dict] = []
    if not lines:
        return records

    # First non-empty line is title; next non-empty is usually author. Notebook often leaves
    # a blank line after the section header, so lines[0] must not be assumed to be the title.
    ti = _next_non_empty_index(lines, 0)
    if ti >= len(lines):
        return records
    title = _norm_line(lines[ti])
    ai = _next_non_empty_index(lines, ti + 1)
    author = ""
    if ai < len(lines):
        cand = _norm_line(lines[ai])
        if not LAST_ACCESSED_PREFIX.match(
            cand
        ) and not STATS_LINE.match(cand):
            author = cand

    stats_i = -1
    for i, line in enumerate(lines):
        if STATS_LINE.match(_norm_line(line)):
            stats_i = i
            break
    if stats_i < 0:
        return records

    last_accessed_iso: str | None = None
    for line in lines[: stats_i + 1]:
        m = LAST_ACCESSED_LINE.match(_norm_line(line))
        if not m:
            continue
        rest = m.group(1).strip()
        try:
            last_accessed_iso = datetime.strptime(rest, "%A, %B %d, %Y").strftime("%Y-%m-%d")
        except ValueError:
            last_accessed_iso = None
        break

    i = stats_i + 1
    while i < len(lines):
        raw = lines[i]
        line = _norm_line(raw)
        if not line:
            i += 1
            continue
        hm = HIGHLIGHT_HEAD.match(line)
        if not hm:
            i += 1
            continue
        color = hm.group("color")
        location = hm.group("loc").replace(",", "").strip()
        i += 1
        if i < len(lines) and _norm_line(lines[i]) == "Options":
            i += 1
        text_lines: list[str] = []
        note: str | None = None
        while i < len(lines):
            nl = _norm_line(lines[i])
            if HIGHLIGHT_HEAD.match(nl):
                break
            if nl.startswith("Note:"):
                note = nl[5:].strip()
                i += 1
                break
            text_lines.append(lines[i].strip())
            i += 1
        text = " ".join(t for t in text_lines if t).strip()
        text = re.sub(r"\s+", " ", text)
        if not text and not note:
            continue
        rid = _stable_id(title, location, text or note or "")
        row: dict = {
            "id": rid,
            "book_title": title,
            "author": author,
            "location": location,
            "color": color,
            "text": text,
            "note": note,
            "source_file": source_file,
            "section_idx": section_idx,
        }
        if last_accessed_iso:
            row["last_accessed"] = last_accessed_iso
        records.append(row)
    return records


def parse_file(path: Path) -> list[dict]:
    raw = path.read_text(encoding="utf-8", errors="replace")
    parts = SECTION_MARK.split(raw)
    all_rows: list[dict] = []
    for idx, part in enumerate(parts[1:], start=1):
        lines = part.splitlines()
        all_rows.extend(parse_section(lines, path.name, idx))
    # Same highlight can repeat across sections (re-captured books); Chroma needs unique ids.
    seen: set[str] = set()
    unique: list[dict] = []
    for row in all_rows:
        rid = row["id"]
        if rid in seen:
            continue
        seen.add(rid)
        unique.append(row)
    return unique


def main() -> None:
    ap = argparse.ArgumentParser(description="Parse Kindle spike_extract_deduped.txt to JSONL")
    ap.add_argument(
        "input",
        type=Path,
        nargs="?",
        default=Path(__file__).resolve().parents[2]
        / "spike"
        / "webview-notebook"
        / "spike_extract_deduped.txt",
        help="Path to deduped extract",
    )
    ap.add_argument(
        "-o",
        "--output",
        type=Path,
        default=Path(__file__).resolve().parents[1] / "data" / "corpus.jsonl",
        help="Output JSONL path",
    )
    args = ap.parse_args()
    if not args.input.is_file():
        raise SystemExit(f"Input not found: {args.input}")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    rows = parse_file(args.input)
    with args.output.open("w", encoding="utf-8") as f:
        for row in rows:
            f.write(json.dumps(row, ensure_ascii=False) + "\n")
    print(f"Wrote {len(rows)} highlights to {args.output}")


if __name__ == "__main__":
    main()
