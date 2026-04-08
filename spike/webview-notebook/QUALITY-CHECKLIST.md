# Spike extract: quality checks (after `spike_extract.txt`)

Use this after capture + **`dedupe_spike_extract.py`** (produces `spike_extract_deduped.txt` and `dedupe_report.txt`).

## Automated

| Check | What to look for |
|--------|-------------------|
| **Dedupe report** | `dedupe_report.txt`: `Dropped duplicates` should match books you re-captured. If zero but you know you duplicated, fingerprints may differ slightly (see below). |
| **Block count** | `Blocks parsed` ≈ number of times you pressed Enter; `Unique bodies` ≤ that. |

## Manual (quick pass)

1. **Open `spike_extract_deduped.txt`** (or `spike_extract.txt` if you skip dedupe).
2. **Spot-check 3–5 books** you care about: confirm **title**, **author**, and several **highlight lines** appear for each distinct book section.
3. **Noise:** Expect sidebar book list + “Options”, “Settings” in `innerText`. That is acceptable for a spike; the MVP parser will strip chrome later.
4. **Preamble:** If you see **ORPHAN PREAMBLE** at the top of `spike_extract_deduped.txt`, that is text that appeared **before** the first `CAPTURE #1` header (e.g. an older run). Review or delete that section if redundant.
5. **Near-duplicates:** If the **same book** was captured twice with **small** UI differences (scroll position, one extra line), **exact** dedupe may **not** merge them. You will see two blocks with similar titles. Manually delete one block or re-run capture for that book once.

## Next engineering steps (post-spike)

1. **Normalize** into structured records: `{book_title, author, highlights: [{location, text, note?}]}`, either DOM parsing in WebView or regex heuristics on this text.
2. **Chunk + embed** for RAG; store **source = book + location**.
3. **Golden questions** (`eval/GOLDEN-QUESTIONS.md` in repo root): run a few queries and verify citations land on real highlights.
4. **Legal/ToS**: confirm comfort with WebView extraction before a public beta.
