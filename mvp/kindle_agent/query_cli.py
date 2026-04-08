"""Query the Kindle corpus: retrieve + synthesize (optional OpenAI)."""

from __future__ import annotations

import argparse
import os
from pathlib import Path

import chromadb
from chromadb.utils import embedding_functions


def synthesize_openai(question: str, passages: list[tuple[str, dict]]) -> tuple[str | None, str | None]:
    """Return (answer, error). error set when key present but API call fails (quota, etc.)."""
    key = os.environ.get("OPENAI_API_KEY")
    if not key:
        return None, None
    try:
        from openai import OpenAI
    except ImportError:
        return None, "openai package missing"
    ctx = "\n\n".join(
        f"[{m.get('book_title', '')} — {m.get('author', '')} — Loc {m.get('location', '')}]\n{d}"
        for d, m in passages
    )
    try:
        client = OpenAI()
        r = client.chat.completions.create(
            model=os.environ.get("OPENAI_MODEL", "gpt-4o-mini"),
            messages=[
                {
                    "role": "system",
                    "content": (
                        "You answer using ONLY the provided reading highlights. "
                        "Cite books by title when you use them. If the passages do not "
                        "support an answer, say so briefly."
                    ),
                },
                {
                    "role": "user",
                    "content": f"Question:\n{question}\n\nPassages:\n{ctx}",
                },
            ],
            temperature=0.3,
        )
        return r.choices[0].message.content, None
    except Exception as exc:  # noqa: BLE001
        return None, str(exc)


def main() -> None:
    ap = argparse.ArgumentParser(description="Ask your Kindle highlights (RAG)")
    ap.add_argument("question", nargs="?", help="Question (or use --interactive)")
    ap.add_argument(
        "-n",
        "--top-k",
        type=int,
        default=8,
        help="Number of chunks to retrieve",
    )
    ap.add_argument(
        "-p",
        "--persist",
        type=Path,
        default=Path(__file__).resolve().parents[1] / "data" / "chroma",
    )
    ap.add_argument(
        "-m",
        "--model",
        default="all-MiniLM-L6-v2",
        help="Same embedding model as ingest",
    )
    ap.add_argument(
        "-i",
        "--interactive",
        action="store_true",
        help="REPL: multiple questions until EOF",
    )
    args = ap.parse_args()

    if not args.persist.is_dir():
        raise SystemExit(
            f"ChromaDB not found at {args.persist}. Run: python -m kindle_agent.ingest"
        )

    ef = embedding_functions.SentenceTransformerEmbeddingFunction(model_name=args.model)
    client = chromadb.PersistentClient(path=str(args.persist))
    name = "kindle_highlights"
    try:
        collection = client.get_collection(name=name, embedding_function=ef)
    except Exception:
        raise SystemExit(
            f"Collection '{name}' missing. Run: python -m kindle_agent.ingest"
        ) from None

    def run(q: str) -> None:
        if not q.strip():
            return
        res = collection.query(query_texts=[q], n_results=args.top_k)
        docs = res["documents"][0] if res["documents"] else []
        metas = res["metadatas"][0] if res["metadatas"] else []
        passages = list(zip(docs, metas))
        print("\n--- Retrieved passages ---")
        for i, (d, m) in enumerate(passages, 1):
            bt = m.get("book_title", "")
            au = (m.get("author") or "").strip()
            loc = m.get("location", "")
            who = f"{bt} — {au}" if au else bt
            print(f"\n[{i}] {who} (Location {loc})\n{d[:1200]}{'…' if len(d) > 1200 else ''}")
        ans, syn_err = synthesize_openai(q, passages)
        print("\n--- Answer ---")
        if ans:
            print(ans)
        elif syn_err:
            print(f"(Synthesis skipped: {syn_err})")
        else:
            print(
                "(Set OPENAI_API_KEY for a synthesized answer. "
                "Above passages are the raw retrieval.)"
            )

    if args.interactive:
        print("Interactive mode. Empty line to exit.")
        while True:
            try:
                q = input("\nQuestion: ").strip()
            except EOFError:
                break
            if not q:
                break
            run(q)
    else:
        if not args.question:
            ap.print_help()
            raise SystemExit(1)
        run(args.question)


if __name__ == "__main__":
    main()
