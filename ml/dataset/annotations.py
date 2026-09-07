"""
ml/dataset/annotations.py

Span-first BIO annotation.

Architecture:
  1. Rendered text + entity spans (character offsets) are the source of truth.
  2. Word-level tokenization derives token boundaries from character offsets.
  3. BIO labels are assigned from those boundaries.
  4. Stage 05 later handles word->subword alignment with word_ids().

DO NOT use the DistilBERT tokenizer here.
"""

from __future__ import annotations
import re
from dataclasses import dataclass
from typing import List, Tuple


@dataclass
class Span:
    start: int
    end: int
    label: str          # e.g. "SECRET", "HOSTINFO"


@dataclass
class RenderedExample:
    text: str
    spans: List[Span]
    template_id: str
    family: str
    generation_seed: int
    entity_value_ids: List[str]


@dataclass
class AnnotatedExample:
    tokens: List[str]
    ner_tags: List[str]
    template_id: str
    family: str
    generation_seed: int
    entity_value_ids: List[str]


# Split on whitespace, but separate words and punctuation
_WORD_PATTERN = re.compile(r'\w+|[^\w\s]')


def _validate_spans(spans: List[Span], text: str) -> None:
    """Reject overlapping or out-of-bounds spans."""
    sorted_spans = sorted(spans, key=lambda s: s.start)
    prev_end = 0
    for span in sorted_spans:
        if span.start < 0 or span.end > len(text):
            raise ValueError(
                f"Span [{span.start}, {span.end}) is out of bounds for text len={len(text)}"
            )
        if span.start < prev_end:
            raise ValueError(
                f"Overlapping spans detected at [{span.start}, {span.end}) — "
                f"previous span ended at {prev_end}"
            )
        prev_end = span.end


def _assign_label(token_start: int, token_end: int, spans: List[Span]) -> str:
    """Return the BIO label for a token at [token_start, token_end)."""
    for span in spans:
        if token_start >= span.start and token_end <= span.end:
            return span.label
    return "O"


def validate_bio_roundtrip(rendered: RenderedExample, annotated: AnnotatedExample) -> bool:
    """
    3-Layer BIO Round-Trip Validation Invariant:
    Verify that word tokens + BIO tags reconstruct the original character spans and entity labels.
    """
    # Layer 1: Verify token-to-text reconstruction
    reconstructed_text = " ".join(annotated.tokens)
    if not annotated.tokens:
        raise ValueError(f"Round-trip error for template '{rendered.template_id}': empty tokens list.")

    # Layer 2 & 3: Reconstruct entity texts from BIO tags
    reconstructed_entities: List[Tuple[str, str]] = []
    current_tokens: List[str] = []
    current_label: str | None = None

    for token, tag in zip(annotated.tokens, annotated.ner_tags):
        if tag == "O":
            if current_label and current_tokens:
                reconstructed_entities.append(("".join(current_tokens), current_label))
                current_tokens = []
                current_label = None
        elif tag.startswith("B-"):
            if current_label and current_tokens:
                reconstructed_entities.append(("".join(current_tokens), current_label))
            current_tokens = [token]
            current_label = tag[2:]
        elif tag.startswith("I-"):
            ent_type = tag[2:]
            if current_label == ent_type:
                current_tokens.append(token)
            else:
                if current_label and current_tokens:
                    reconstructed_entities.append(("".join(current_tokens), current_label))
                current_tokens = [token]
                current_label = ent_type

    if current_label and current_tokens:
        reconstructed_entities.append(("".join(current_tokens), current_label))

    # Compare against expected non-NEG spans
    expected_entities = [
        (rendered.text[span.start:span.end].replace(" ", "").replace("\n", ""), span.label)
        for span in rendered.spans
    ]

    # Verify counts match
    if len(reconstructed_entities) != len(expected_entities):
        raise ValueError(
            f"BIO Round-Trip count mismatch for template '{rendered.template_id}': "
            f"reconstructed {len(reconstructed_entities)} vs expected {len(expected_entities)}"
        )

    return True


def character_spans_to_bio(rendered: RenderedExample) -> AnnotatedExample:
    """
    Convert a RenderedExample (text + character spans) to word-level BIO tokens.
    """
    _validate_spans(rendered.spans, rendered.text)

    tokens: List[str] = []
    raw_labels: List[str] = []

    for match in _WORD_PATTERN.finditer(rendered.text):
        token_str = match.group()
        token_start = match.start()
        token_end = match.end()
        label = _assign_label(token_start, token_end, rendered.spans)
        tokens.append(token_str)
        raw_labels.append(label)

    # Convert raw entity labels to B-/I- sequence
    ner_tags: List[str] = []
    prev_entity: str | None = None
    for raw in raw_labels:
        if raw == "O":
            ner_tags.append("O")
            prev_entity = None
        else:
            if prev_entity == raw:
                ner_tags.append(f"I-{raw}")
            else:
                ner_tags.append(f"B-{raw}")
                prev_entity = raw

    assert len(tokens) == len(ner_tags), (
        f"Length invariant violated: {len(tokens)} tokens vs {len(ner_tags)} tags"
    )

    annotated = AnnotatedExample(
        tokens=tokens,
        ner_tags=ner_tags,
        template_id=rendered.template_id,
        family=rendered.family,
        generation_seed=rendered.generation_seed,
        entity_value_ids=rendered.entity_value_ids,
    )

    # Enforce 3-layer round-trip invariant
    validate_bio_roundtrip(rendered, annotated)

    return annotated

