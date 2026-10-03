"""BiRefNet alpha mattes without the ComfyUI server: run with ComfyUI's own Python (same weights as the server node).

  "S:/AI/ComfyUI_windows_portable/ComfyUI-Easy-Install/python_embeded/python.exe" Tools/matte_frames.py <list.txt>

<list.txt> holds one "input.png|output_alpha.png" pair per line. produce_fighter_clips.py writes the list and calls
this when the server is not reachable. Paths go in a file because ComfyUI parses the command line itself.
"""
import os
import pathlib
import sys

import numpy as np
from PIL import Image

COMFY = pathlib.Path('S:/AI/ComfyUI_windows_portable/ComfyUI-Easy-Install/ComfyUI')
WEIGHTS = COMFY / 'models' / 'background_removal' / 'birefnet.safetensors'

pairs = [line.split('|') for line in pathlib.Path(sys.argv[1]).read_text(encoding='utf-8').splitlines() if '|' in line]
sys.argv = sys.argv[:1]
sys.path.insert(0, str(COMFY))
os.chdir(COMFY)
import torch  # noqa: E402
import comfy.bg_removal_model  # noqa: E402

model = comfy.bg_removal_model.load(str(WEIGHTS))
for src, dst in pairs:
    img = Image.open(src).convert('RGB')
    image = torch.from_numpy(np.asarray(img, dtype=np.float32) / 255.0).unsqueeze(0)
    with torch.no_grad():
        mask = model.encode_image(image)[0].float().cpu().numpy()
    Image.fromarray((np.clip(mask, 0, 1) * 255 + .5).astype(np.uint8)).save(dst)
print('matted', len(pairs), flush=True)
