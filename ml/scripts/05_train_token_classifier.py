import argparse
import json
import os
import sys
from pathlib import Path
import numpy as np
import torch
from datasets import load_dataset
import transformers
import evaluate as evaluate_lib
from transformers import (
    DistilBertTokenizerFast,
    DistilBertForTokenClassification,
    Trainer,
    TrainingArguments,
    DataCollatorForTokenClassification,
    EarlyStoppingCallback,
    set_seed
)

BASE_DIR = Path(__file__).resolve().parent.parent
SPLITS_DIR = BASE_DIR / "data" / "splits"
CHECKPOINT_DIR = BASE_DIR / "models" / "checkpoints"
FINAL_MODEL_DIR = CHECKPOINT_DIR / "final_model"

# Authoritative 9-label contract
LABELS = [
    "O",
    "B-SECRET",
    "I-SECRET",
    "B-PII",
    "I-PII",
    "B-HOSTINFO",
    "I-HOSTINFO",
    "B-NETWORK",
    "I-NETWORK",
]
label2id = {l: i for i, l in enumerate(LABELS)}
id2label = {i: l for i, l in enumerate(LABELS)}
MAX_LENGTH = 128


def print_environment_info():
    print("=== Environment Verification ===")
    print(f"Python Exec:  {sys.executable}")
    print(f"Python:       {sys.version.split()[0]}")
    print(f"PyTorch:      {torch.__version__} ({getattr(torch, '__file__', 'unknown')})")
    print(f"Transformers: {transformers.__version__} ({getattr(transformers, '__file__', 'unknown')})")
    print(f"CUDA Available: {torch.cuda.is_available()}")
    if torch.cuda.is_available():
        gpu_name = torch.cuda.get_device_name(0)
        gpu_mem_gb = torch.cuda.get_device_properties(0).total_memory / (1024**3)
        print(f"Device:       {gpu_name} ({gpu_mem_gb:.2f} GB VRAM)")
        print(f"CUDA Version: {torch.version.cuda}")
    else:
        print("Device:       CPU")
    print("================================")


def validate_raw_dataset(dataset):
    print("\n=== Validating Dataset Schema & Splits ===")
    total_invalid = 0
    
    for split_name in ["train", "val"]:
        split = dataset[split_name]
        print(f"Checking '{split_name}' split ({len(split)} examples)...")
        
        for idx, row in enumerate(split):
            if "tokens" not in row or "ner_tags" not in row:
                raise ValueError(f"Split '{split_name}' record #{idx} is missing 'tokens' or 'ner_tags' fields.")
            
            tokens = row["tokens"]
            tags = row["ner_tags"]
            
            if len(tokens) != len(tags):
                print(f"[ERROR] Mismatch at index {idx} in {split_name}: {len(tokens)} tokens vs {len(tags)} tags.")
                total_invalid += 1
                
            for tag in tags:
                if tag not in label2id:
                    raise ValueError(f"Unknown label '{tag}' found at record #{idx} in '{split_name}'.")

    if total_invalid > 0:
        raise ValueError(f"Dataset validation failed with {total_invalid} corrupted records.")
    print("Schema check: PASS (tokens and ner_tags lengths match across all records)")


def tokenize_and_align_labels(examples, tokenizer):
    tokenized_inputs = tokenizer(
        examples["tokens"],
        truncation=True,
        is_split_into_words=True,
        max_length=MAX_LENGTH,
    )

    all_labels = []
    for i, labels in enumerate(examples["ner_tags"]):
        word_ids = tokenized_inputs.word_ids(batch_index=i)
        previous_word_id = None
        label_ids = []

        for word_id in word_ids:
            if word_id is None:
                # Special tokens ([CLS], [SEP], padding) are ignored by loss
                label_ids.append(-100)
            elif word_id != previous_word_id:
                # First subtoken of a word takes the original BIO label
                label_ids.append(label2id[labels[word_id]])
            else:
                # Subsequent subtokens convert B-X into I-X
                original_label = labels[word_id]
                if original_label.startswith("B-"):
                    original_label = "I-" + original_label[2:]
                label_ids.append(label2id[original_label])

            previous_word_id = word_id

        all_labels.append(label_ids)

    tokenized_inputs["labels"] = all_labels
    return tokenized_inputs


def run_preflight_smoke_check(sample, tokenizer):
    print("\n=== Pre-Training Preprocessing & Alignment Check ===")
    tokenized = tokenize_and_align_labels(
        {"tokens": [sample["tokens"]], "ner_tags": [sample["ner_tags"]]}, 
        tokenizer
    )
    
    input_ids = tokenized["input_ids"][0]
    attention_mask = tokenized["attention_mask"][0]
    labels = tokenized["labels"][0]
    
    assert len(input_ids) == len(attention_mask) == len(labels), \
        f"Length invariant failed: input_ids={len(input_ids)}, mask={len(attention_mask)}, labels={len(labels)}"
    
    tokens = tokenizer.convert_ids_to_tokens(input_ids)
    print(f"Sample word tokens: {sample['tokens']}")
    print(f"Sample BIO tags:    {sample['ner_tags']}")
    print("Aligned Subwords:")
    for t, l_id in zip(tokens, labels):
        l_name = id2label[l_id] if l_id != -100 else "-100 (IGNORED)"
        print(f"  {t:<15} -> {l_name} (ID: {l_id})")
    print("Alignment check: PASS")


def check_truncation(dataset, tokenizer):
    truncated_count = 0
    max_tokens_seen = 0
    
    for row in dataset["train"]:
        full_tokens = tokenizer(row["tokens"], is_split_into_words=True)
        length = len(full_tokens["input_ids"])
        if length > max_tokens_seen:
            max_tokens_seen = length
        if length > MAX_LENGTH:
            truncated_count += 1
            
    print(f"\nTruncation Analysis (max_length={MAX_LENGTH}):")
    print(f"  Maximum token length encountered: {max_tokens_seen}")
    print(f"  Truncated examples:               {truncated_count} of {len(dataset['train'])}")


def get_git_commit() -> str:
    import subprocess
    try:
        return subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip()
    except Exception:
        return "unknown"


def main() -> None:
    parser = argparse.ArgumentParser(description="DistilBERT token classifier training")
    parser.add_argument(
        "--mode", choices=["fresh", "resume", "evaluate"], default="fresh",
        help="Training mode: 'fresh' (new timestamped run dir), 'resume' (resume checkpoint), or 'evaluate'"
    )
    parser.add_argument(
        "--fresh", action="store_const", dest="mode", const="fresh",
        help="Alias for --mode fresh"
    )
    parser.add_argument(
        "--resume", action="store_const", dest="mode", const="resume",
        help="Alias for --mode resume"
    )
    parser.add_argument(
        "--evaluate", action="store_const", dest="mode", const="evaluate",
        help="Alias for --mode evaluate"
    )
    parser.add_argument(
        "--dry-run", action="store_true",
        help="Run preprocessing checks only — skip model loading and Trainer"
    )
    args = parser.parse_args()

    print_environment_info()
    set_seed(42)  # Ensure reproducibility

    from datetime import datetime, timezone
    now_str = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
    
    if args.mode == "fresh":
        run_id = f"run-{now_str}"
        active_output_dir = CHECKPOINT_DIR / run_id
        print(f"\nTraining Mode  : FRESH")
        print(f"Run Directory  : {active_output_dir}")
    elif args.mode == "resume":
        run_id = "resumed"
        active_output_dir = CHECKPOINT_DIR
        print(f"\nTraining Mode  : RESUME")
        print(f"Run Directory  : {active_output_dir}")
    else:
        run_id = "eval"
        active_output_dir = CHECKPOINT_DIR
        print(f"\nTraining Mode  : EVALUATE ONLY")

    active_output_dir.mkdir(parents=True, exist_ok=True)

    train_path = SPLITS_DIR / "train.jsonl"
    val_path = SPLITS_DIR / "val.jsonl"

    if not train_path.exists() or not val_path.exists():
        raise FileNotFoundError(
            f"Missing dataset files in {SPLITS_DIR}. "
            f"Run: python ml/scripts/04_generate_dataset.py"
        )

    # Calculate dataset SHA256
    import hashlib
    with open(train_path, "rb") as f:
        dataset_sha256 = hashlib.sha256(f.read()).hexdigest()

    dataset = load_dataset("json", data_files={
        "train": str(train_path),
        "val": str(val_path)
    })

    validate_raw_dataset(dataset)

    tokenizer = DistilBertTokenizerFast.from_pretrained("distilbert-base-uncased")

    run_preflight_smoke_check(dataset["train"][0], tokenizer)
    check_truncation(dataset, tokenizer)

    print("\nTokenizing train and validation sets...")
    tokenized_dataset = dataset.map(
        lambda x: tokenize_and_align_labels(x, tokenizer),
        batched=True,
        remove_columns=dataset["train"].column_names
    )
    print(f"  Tokenized train: {len(tokenized_dataset['train'])} examples")
    print(f"  Tokenized val  : {len(tokenized_dataset['val'])} examples")

    model = DistilBertForTokenClassification.from_pretrained(
        "distilbert-base-uncased",
        num_labels=len(LABELS),
        id2label=id2label,
        label2id=label2id
    )

    # Verify model config
    assert model.config.num_labels == 9, f"Expected 9 labels, got {model.config.num_labels}"
    print(f"  Model num_labels : {model.config.num_labels} [OK]")
    print(f"  id2label sample  : {dict(list(model.config.id2label.items())[:3])}")

    data_collator = DataCollatorForTokenClassification(tokenizer=tokenizer, padding=True, pad_to_multiple_of=8)

    def compute_metrics(p):
        return {}

    num_epochs = 6
    training_args_kwargs = {
        "output_dir":                   str(active_output_dir),
        "learning_rate":                2e-5,
        "per_device_train_batch_size":  8,       # conservative for 6 GB VRAM
        "per_device_eval_batch_size":   8,
        "gradient_accumulation_steps": 2,        # effective batch = 16
        "num_train_epochs":             num_epochs,
        "weight_decay":                 0.01,
        "max_grad_norm":                1.0,     # Gradient clipping
        "logging_steps":               25,
        "save_strategy":               "epoch",
        "save_total_limit":             2,       # Keep only the 2 best checkpoints
        "fp16":                        torch.cuda.is_available(),
        "load_best_model_at_end":      True,
        "metric_for_best_model":       "eval_loss",
        "greater_is_better":           False,
    }

    if hasattr(TrainingArguments, "eval_strategy"):
        training_args_kwargs["eval_strategy"] = "epoch"
    else:
        training_args_kwargs["evaluation_strategy"] = "epoch"

    training_args = TrainingArguments(**training_args_kwargs)

    import inspect
    trainer_sig = inspect.signature(Trainer.__init__).parameters
    proc_key = "processing_class" if "processing_class" in trainer_sig else "tokenizer"
    print(f"  Trainer API key  : {proc_key}")

    trainer = Trainer(
        model=model,
        args=training_args,
        train_dataset=tokenized_dataset["train"],
        eval_dataset=tokenized_dataset["val"],
        data_collator=data_collator,
        compute_metrics=compute_metrics,
        callbacks=[EarlyStoppingCallback(early_stopping_patience=3)],
        **{proc_key: tokenizer},
    )

    # --- Forward-pass precheck ---
    print("\n=== Forward-Pass Precheck ===")
    model_device = "cuda" if torch.cuda.is_available() else "cpu"
    model.to(model_device)
    model.eval()
    with torch.no_grad():
        sample_batch = data_collator([tokenized_dataset["train"][i] for i in range(4)])
        def _to_device(key):
            t = sample_batch[key]
            return (t.clone().detach() if isinstance(t, torch.Tensor)
                    else torch.tensor(t)).to(model_device)
        input_ids      = _to_device("input_ids")
        attention_mask = _to_device("attention_mask")
        labels         = _to_device("labels")
        out = model(input_ids=input_ids, attention_mask=attention_mask, labels=labels)
        logits_shape = tuple(out.logits.shape)
        assert torch.isfinite(out.loss), f"Loss is not finite: {out.loss}"
        assert logits_shape[2] == 9, f"Expected last dim=9, got {logits_shape}"
        print(f"  Device           : {model_device}")
        print(f"  Model params on  : {next(model.parameters()).device}")
        print(f"  Logits shape     : {logits_shape} [OK]")
        print(f"  Loss (precheck)  : {out.loss.item():.4f} [OK]")
    print("Forward-Pass Precheck: PASS")

    if args.dry_run or args.mode == "evaluate":
        print(f"\n=== DRY RUN / EVALUATION MODE COMPLETE ===")
        return

    model.train()

    print("\n=== Beginning Model Training ===")
    print(f"  Device           : {model_device}")
    print(f"  Mode             : {args.mode}")
    print(f"  Batch size       : 8 (effective 16 with grad_accum=2)")
    
    from transformers.trainer_utils import get_last_checkpoint
    last_checkpoint = None
    if args.mode == "resume":
        last_checkpoint = get_last_checkpoint(str(active_output_dir))
        if last_checkpoint is not None:
            # Completed checkpoint detection
            trainer_state_file = Path(last_checkpoint) / "trainer_state.json"
            if trainer_state_file.exists():
                with open(trainer_state_file, "r", encoding="utf-8") as f:
                    state_data = json.load(f)
                checkpoint_step = state_data.get("global_step", 0)
                checkpoint_epoch = state_data.get("epoch", 0.0)
                if checkpoint_epoch >= num_epochs:
                    raise RuntimeError(
                        f"Checkpoint '{last_checkpoint}' has already completed all {num_epochs} epochs "
                        f"(step {checkpoint_step}). Cannot resume completed run. Use --fresh instead."
                    )
            print(f"  Resuming from    : {last_checkpoint}")
        else:
            print("  No checkpoint found to resume. Starting fresh.")

    training_start_time = datetime.now(timezone.utc).isoformat()
    before_step = trainer.state.global_step

    train_result = trainer.train(resume_from_checkpoint=last_checkpoint)
    
    after_step = trainer.state.global_step
    steps_performed = after_step - before_step

    print(f"\n=== Training Execution Guard Check ===")
    print(f"  Starting Step    : {before_step}")
    print(f"  Ending Step      : {after_step}")
    print(f"  Steps Performed  : {steps_performed}")
    print(f"  Train Runtime    : {train_result.metrics.get('train_runtime', 0):.2f} sec")
    print(f"  Train Loss       : {train_result.metrics.get('train_loss', 0):.4f}")

    if steps_performed <= 0:
        raise RuntimeError(
            f"QUALITY GATE FAILURE: Training performed {steps_performed} optimizer steps. "
            f"Start step={before_step}, End step={after_step}."
        )

    print("Step Assertion Check: PASS (optimizer steps > 0)")

    # Save best_model (loaded automatically by load_best_model_at_end=True)
    best_model_dir = active_output_dir / "best_model"
    target_final_dir = active_output_dir / "final_model"

    print(f"\nSaving best_model & tokenizer artifacts...")
    trainer.save_model(str(best_model_dir))
    tokenizer.save_pretrained(str(best_model_dir))
    trainer.save_model(str(target_final_dir))
    tokenizer.save_pretrained(str(target_final_dir))

    # Sync best_model to canonical FINAL_MODEL_DIR for evaluation & export tools
    FINAL_MODEL_DIR.mkdir(parents=True, exist_ok=True)
    trainer.save_model(str(FINAL_MODEL_DIR))
    tokenizer.save_pretrained(str(FINAL_MODEL_DIR))

    training_finish_time = datetime.now(timezone.utc).isoformat()
    status_label = "COMPLETED" if after_step >= (num_epochs * (len(tokenized_dataset["train"]) // 16)) else "EARLY_STOPPED"

    summary_data = {
        "status": status_label,
        "run_id": run_id,
        "mode": args.mode,
        "base_model": "distilbert-base-uncased",
        "git_commit": get_git_commit(),
        "dataset_sha256": dataset_sha256,
        "num_labels": len(LABELS),
        "label2id": label2id,
        "train_examples": len(dataset["train"]),
        "val_examples": len(dataset["val"]),
        "per_device_batch_size": 8,
        "gradient_accumulation_steps": 2,
        "effective_batch_size": 16,
        "num_epochs": num_epochs,
        "starting_global_step": before_step,
        "ending_global_step": after_step,
        "optimizer_steps_performed": steps_performed,
        "train_runtime_sec": train_result.metrics.get("train_runtime", 0),
        "train_loss": train_result.metrics.get("train_loss", 0),
        "best_model_checkpoint": trainer.state.best_model_checkpoint,
        "best_metric": trainer.state.best_metric,
        "training_started_at": training_start_time,
        "training_finished_at": training_finish_time,
        "device": torch.cuda.get_device_name(0) if torch.cuda.is_available() else "CPU"
    }

    for meta_path in [best_model_dir / "training_summary.json", target_final_dir / "training_summary.json", FINAL_MODEL_DIR / "training_summary.json"]:
        with open(meta_path, "w", encoding="utf-8") as f:
            json.dump(summary_data, f, indent=2)

    print(f"Saved metadata to training_summary.json (status={status_label}).")

    print("\nNEXT STEP:")
    print("  python ml/scripts/verify_checkpoint.py")


if __name__ == "__main__":
    main()
