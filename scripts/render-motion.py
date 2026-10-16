"""Encode frames rendered by the actual WPF window; no artwork from the references is used."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parent.parent
source = root / "artifacts" / "verification" / "motion"
paths = sorted(source.glob("frame-*.png"))
if not paths:
    raise SystemExit("Run verify.ps1 -Published first.")
frames = [Image.open(path).convert("RGB") for path in paths]
durations = [33] * len(frames)
durations[13] = 550
durations[33] = 700
durations[-1] = 550
output = source.parent / "luma-motion.gif"
frames[0].save(output, save_all=True, append_images=frames[1:], duration=durations, loop=0, optimize=False, disposal=2)
print(output)
