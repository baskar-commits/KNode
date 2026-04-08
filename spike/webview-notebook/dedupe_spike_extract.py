"""
Deduplicate spike_extract.txt capture blocks.

- Splits on CAPTURE #n headers (lines of 80 '=').
- Fingerprint: SHA-256 of whitespace-normalized body text (exact duplicate captures).
- Keeps first occurrence; writes spike_extract_deduped.txt + dedupe_report.txt.

Run: .venv\\Scripts\\python.exe dedupe_spike_extract.py
"""

from __future__ import annotations

import hashlib
import re
from pathlib import Path

SEP = "=" * 80
INPUT_FILE = Path(__file__).resolve().parent / "spike_extract.txt"
OUT_FILE = Path(__file__).resolve().parent / "spike_extract_deduped.txt"
REPORT_FILE = Path(__file__).resolve().parent / "dedupe_report.txt"


def normalize_for_fingerprint(text: str) -> str:
    """Collapse whitespace so trivial spacing diffs still match."""
    return re.sub(r"\s+", " ", text.strip())


def fingerprint(text: str) -> str:
    return hashlib.sha256(normalize_for_fingerprint(text).encode("utf-8")).hexdigest()


def parse_blocks(raw: str) -> tuple[str, list[dict]]:
    """Return (preamble before first CAPTURE, list of {num, header_lines, body})."""
    lines = raw.splitlines()
    preamble: list[str] = []
    blocks: list[dict] = []
    i = 0

    while i < len(lines):
        if (
            lines[i] == SEP
            and i + 1 < len(lines)
            and lines[i + 1].startswith("CAPTURE #")
        ):
            cap_line = lines[i + 1]
            m = re.match(r"CAPTURE #(\d+)", cap_line)
            num = int(m.group(1)) if m else -1
            i += 2
            meta_lines: list[str] = [cap_line]
            while i < len(lines) and lines[i] != SEP:
                meta_lines.append(lines[i])
                i += 1
            if i < len(lines) and lines[i] == SEP:
                i += 1
            body_start = i
            while i < len(lines):
                if (
                    lines[i] == SEP
                    and i + 1 < len(lines)
                    and lines[i + 1].startswith("CAPTURE #")
                ):
                    break
                i += 1
            body = "\n".join(lines[body_start:i])
            blocks.append({"num": num, "meta": "\n".join(meta_lines), "body": body})
        else:
            preamble.append(lines[i])
            i += 1

    return "\n".join(preamble).strip(), blocks


def format_block(block: dict, new_index: int) -> str:
    """Rebuild one capture with a stable header (re-number)."""
    return (
        f"{SEP}\n"
        f"DEDUPED CAPTURE #{new_index} (original CAPTURE #{block['num']})\n"
        f"{block['meta']}\n"
        f"{SEP}\n\n"
        f"{block['body']}\n"
    )


def main() -> None:
    if not INPUT_FILE.is_file():
        raise SystemExit(f"Missing {INPUT_FILE}")

    raw = INPUT_FILE.read_text(encoding="utf-8", errors="replace")
    preamble, blocks = parse_blocks(raw)

    seen: dict[str, dict] = {}
    order_kept: list[dict] = []
    dropped: list[tuple[int, str, str]] = []

    for b in blocks:
        fp = fingerprint(b["body"])
        if fp not in seen:
            seen[fp] = b
            order_kept.append(b)
        else:
            first_num = seen[fp]["num"]
            dropped.append((b["num"], fp[:16], f"duplicate of original CAPTURE #{first_num}"))

    out_parts: list[str] = []
    if preamble:
        out_parts.append(
            f"{SEP}\nORPHAN PREAMBLE (content before first CAPTURE header — review manually)\n{SEP}\n\n{preamble}\n\n"
        )
    for idx, b in enumerate(order_kept, start=1):
        out_parts.append(format_block(b, idx))

    OUT_FILE.write_text("".join(out_parts), encoding="utf-8")

    report_lines = [
        f"Input:  {INPUT_FILE.name}",
        f"Blocks parsed: {len(blocks)}",
        f"Unique bodies: {len(order_kept)}",
        f"Dropped duplicates: {len(dropped)}",
        "",
        "Dropped (original capture #, fp prefix, reason):",
    ]
    for t in dropped:
        report_lines.append(f"  CAPTURE #{t[0]}  fp={t[1]}...  {t[2]}")
    report_lines.append("")
    report_lines.append(f"Output: {OUT_FILE.name}")
    REPORT_FILE.write_text("\n".join(report_lines), encoding="utf-8")

    print("\n".join(report_lines))


if __name__ == "__main__":
    main()
