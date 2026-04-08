"""
Step-1 spike: Option B — embedded WebView (Edge WebView2 via pywebview on Windows).

Flow:
  1. Opens https://read.amazon.com/notebook in an in-app browser.
  2. You sign in to Amazon in the window (same as a normal browser tab).
  3. Terminal loops: select a book in the WebView, then press Enter to append that
     view to spike_extract.txt. Repeat per book. Type q + Enter to stop.

Success for this spike: extraction appends non-empty text blocks to spike_extract.txt.
Delete spike_extract.txt before a run if you want a fresh file.

Requires: WebView2 Runtime (usually preinstalled on Windows 10/11).
"""

from __future__ import annotations

import json
import threading
from datetime import datetime, timezone
from pathlib import Path

import webview

OUT_FILE = Path(__file__).resolve().parent / "spike_extract.txt"
META_FILE = Path(__file__).resolve().parent / "spike_extract_meta.jsonl"


def _extract_js() -> str:
    """Return JS that collects debug info + visible text (best-effort, DOM may change)."""
    return """
    (function () {
      var title = document.title || '';
      var url = location.href || '';
      var bodyText = '';
      try {
        bodyText = document.body ? document.body.innerText : '';
      } catch (e) {
        bodyText = 'innerText error: ' + e;
      }
      return JSON.stringify({
        title: title,
        url: url,
        textLength: bodyText.length,
        textSample: bodyText.slice(0, 8000),
        fullText: bodyText
      });
    })();
    """


def run_extraction(window: webview.Window, capture_num: int) -> None:
    try:
        raw = window.evaluate_js(_extract_js())
        if raw is None:
            print("evaluate_js returned None — page may not be ready or JS blocked.")
            return
        data = json.loads(raw)
        full = data.get("fullText") or ""
        ts = datetime.now(timezone.utc).astimezone().isoformat(timespec="seconds")
        header = (
            f"\n{'=' * 80}\n"
            f"CAPTURE #{capture_num} | {ts}\n"
            f"Document title: {data.get('title') or '(empty)'}\n"
            f"URL: {data.get('url')}\n"
            f"Characters (this block): {len(full)}\n"
            f"{'=' * 80}\n\n"
        )
        with OUT_FILE.open("a", encoding="utf-8") as f:
            f.write(header + full + "\n")
        meta = {
            "capture": capture_num,
            "at": ts,
            "title": data.get("title"),
            "url": data.get("url"),
            "textLength": data.get("textLength"),
        }
        with META_FILE.open("a", encoding="utf-8") as f:
            f.write(json.dumps(meta) + "\n")
        print(f"Appended capture #{capture_num}: {len(full)} characters → {OUT_FILE.name}")
        print(f"Meta: {meta}")
    except Exception as exc:  # noqa: BLE001 — spike script
        print(f"Extraction failed: {exc}")


def _prompt_and_extract(window: webview.Window) -> None:
    print()
    print("-" * 60)
    print("WebView: sign in and open Kindle Notebook.")
    print("For each book: select it in the left nav, wait for highlights,")
    print("then return here.")
    print("Each empty line + Enter appends that book to the SAME spike_extract.txt.")
    print("Type q then Enter to stop capturing (you can close the window after).")
    print("-" * 60)
    n = 0
    while True:
        try:
            line = input("\nSelect book in WebView, then Enter=capture | q=quit: ")
        except EOFError:
            print("EOF — stopping capture loop.")
            return
        if line.strip().lower() == "q":
            print("Done capturing.")
            return
        n += 1
        run_extraction(window, n)


def main() -> None:
    window = webview.create_window(
        "Kindle Notebook spike (Option B — WebView)",
        "https://read.amazon.com/notebook",
        width=1280,
        height=900,
    )
    threading.Thread(target=_prompt_and_extract, args=(window,), daemon=True).start()
    # debug=True opens devtools on supported platforms (helps inspect DOM/network).
    webview.start(debug=True)


if __name__ == "__main__":
    main()
