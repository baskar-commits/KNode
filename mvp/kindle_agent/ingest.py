"""Load corpus.jsonl into ChromaDB with sentence-transformer embeddings."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import chromadb
from chromadb.utils import embedding_functions


def main() -> None:
    ap = argparse.ArgumentParser(description="Embed corpus into ChromaDB")
    ap.add_argument(
        "-i",
        "--input",
        type=Path,
        default=Path(__file__).resolve().parents[1] / "data" / "corpus.jsonl",
    )
    ap.add_argument(
        "-p",
        "--persist",
        type=Path,
        default=Path(__file__).resolve().parents[1] / "data" / "chroma",
        help="Chroma persistence directory",
    )
    ap.add_argument(
        "-m",
        "--model",
        default="all-MiniLM-L6-v2",
        help="Sentence-transformers model name",
    )
    ap.add_argument(
        "--batch",
        type=int,
        default=500,
        help="Batch size for add()",
    )
    args = ap.parse_args()
    if not args.input.is_file():
        raise SystemExit(f"Missing corpus: {args.input}")

    rows: list[dict] = []
    with args.input.open(encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                rows.append(json.loads(line))

    if not rows:
        raise SystemExit("Corpus is empty")

    args.persist.mkdir(parents=True, exist_ok=True)
    ef = embedding_functions.SentenceTransformerEmbeddingFunction(model_name=args.model)
    client = chromadb.PersistentClient(path=str(args.persist))
    name = "kindle_highlights"
    try:
        client.delete_collection(name)
    except Exception:  # noqa: BLE001
        pass
    collection = client.create_collection(name=name, embedding_function=ef)

    # Chroma metadata: flat str only
    ids: list[str] = []
    documents: list[str] = []
    metadatas: list[dict] = []

    for r in rows:
        hid = r["id"]
        text = r["text"]
        if r.get("note"):
            text = f"{text}\n[Your note: {r['note']}]".strip()
        if not text:
            continue
        doc = f"{r['book_title']}\n{r.get('author', '')}\n{text}"
        ids.append(hid)
        documents.append(doc)
        metadatas.append(
            {
                "book_title": r["book_title"][:2000],
                "author": (r.get("author") or "")[:2000],
                "location": str(r.get("location", ""))[:200],
                "highlight_id": hid[:200],
            }
        )

    for start in range(0, len(ids), args.batch):
        end = min(start + args.batch, len(ids))
        collection.add(
            ids=ids[start:end],
            documents=documents[start:end],
            metadatas=metadatas[start:end],
        )
    print(f"Ingested {len(ids)} documents into {args.persist} (collection={name})")


if __name__ == "__main__":
    main()
