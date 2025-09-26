#!/usr/bin/env python3
"""
Convert a Keras (.keras / .h5) model to ONNX using tf2onnx and validate it.
 
Usage:
  python convert_keras_to_onnx.py /path/to/model.keras /path/to/model.onnx --opset 15 --overwrite
 
Requires:
  pip install tensorflow keras tf2onnx onnx
  # If you saw the protobuf "Descriptors cannot be created directly" error,
  # also run: pip install "protobuf==3.20.3"
"""
 
from __future__ import annotations
import argparse, sys, tempfile, traceback
from pathlib import Path
 
# Keras import (Keras 3 or fallback to tf.keras on older stacks)
try:
    from keras.models import load_model  # Keras 3
except Exception:
    from tensorflow.keras.models import load_model  # pragma: no cover
 
import onnx
import tensorflow as tf
import tf2onnx
 
SUPPORTED_IN_EXTS = {".keras", ".h5", ".hdf5"}
 
def fail(msg: str, code: int = 1):
    print(f"[ERROR] {msg}", file=sys.stderr)
    sys.exit(code)
 
def warn(msg: str):
    print(f"[WARN] {msg}", file=sys.stderr)
 
def info(msg: str):
    print(f"[INFO] {msg}")
 
def validate_input_file(p: Path):
    if not p.exists():
        fail(f"Input file does not exist: {p}")
    if not p.is_file():
        fail(f"Input path is not a file: {p}")
    if p.suffix.lower() not in SUPPORTED_IN_EXTS:
        warn(f"Unexpected extension '{p.suffix}'. Expected one of {sorted(SUPPORTED_IN_EXTS)}.")
 
def resolve_output_path(p: Path, overwrite: bool) -> Path:
    if p.suffix.lower() != ".onnx":
        warn(f"Output file '{p}' has no .onnx extension; appending '.onnx'.")
        p = p.with_suffix(".onnx")
    p.parent.mkdir(parents=True, exist_ok=True)
    if p.exists() and not overwrite:
        fail(f"Output already exists and --overwrite not set: {p}", code=2)
    return p
 
def convert_with_tf2onnx(input_path: Path, output_path: Path, opset: int | None):
    info(f"Loading Keras model: {input_path}")
    try:
        model = load_model(str(input_path))
    except Exception:
        traceback.print_exc()
        fail("Failed to load the Keras model. Check Keras/TF versions and custom layers.")
 
    # Build a minimal input signature (assumes single input; adapt if needed)
    try:
        inputs = model.inputs
        if not inputs:
            fail("Model has no inputs; cannot infer input signature.")
        spec = (tf.TensorSpec(inputs[0].shape, inputs[0].dtype, name=getattr(inputs[0], "name", "input_0")),)
    except Exception:
        traceback.print_exc()
        fail("Could not infer an input signature from the Keras model.")
 
    info(f"Converting to ONNX via tf2onnx (opset={opset or 'default'})")
    try:
        onnx_model, _ = tf2onnx.convert.from_keras(
            model,
            input_signature=spec,
            opset=opset,
            output_path=None,   # we'll save manually after validation
        )
    except Exception:
        traceback.print_exc()
        fail("tf2onnx conversion failed. Check tf2onnx/TensorFlow/Keras compatibility.")
 
    info(f"Saving ONNX model: {output_path}")
    onnx.save(onnx_model, str(output_path))
 
def validate_onnx(output_path: Path):
    info(f"Validating ONNX structure: {output_path}")
    try:
        m = onnx.load(str(output_path))
        onnx.checker.check_model(m)
    except Exception:
        traceback.print_exc()
        fail("ONNX validation failed. The exported file is not a valid ONNX graph.")
    info("ONNX validation passed ✅")
 
def main():
    ap = argparse.ArgumentParser(description="Convert Keras model to ONNX with tf2onnx and validate it.")
    ap.add_argument("input", help="Path to input .keras / .h5")
    ap.add_argument("output", help="Path to output .onnx")
    ap.add_argument("--opset", type=int, default=None, help="Target ONNX opset (e.g., 13, 15, 17).")
    ap.add_argument("--overwrite", action="store_true", help="Overwrite existing output.")
    args = ap.parse_args()
 
    in_p = Path(args.input).expanduser().resolve()
    out_p = Path(args.output).expanduser().resolve()
 
    validate_input_file(in_p)
    out_p = resolve_output_path(out_p, overwrite=args.overwrite)
 
    convert_with_tf2onnx(in_p, out_p, opset=args.opset)
    validate_onnx(out_p)
    info(f"Conversion successful! ONNX saved at: {out_p}")
 
if __name__ == "__main__":
    main()
