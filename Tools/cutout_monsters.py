"""Cut monster paintings out of their backgrounds for the battle field (Resources/AdamsHaven/FieldModels).

Uses ComfyUI's built-in BiRefNet (weights already in the Easy-Install models folder), run headless with
ComfyUI's own Python. Ids go in an environment variable because ComfyUI parses the command line itself:
  set CUTOUT_IDS=glasswing_mite quartzback_hound
  "S:/AI/ComfyUI_windows_portable/ComfyUI-Easy-Install/python_embeded/python.exe" Tools/cutout_monsters.py

Source: S:/AI/Game/Game Assets/monsters/monster/<id>.png (768x896 paintings from the Godot prototype).
Writes FieldModels/<id>.png (RGBA cutout, same size) and Enemies/<id>.png (the untouched painting),
matching the species already in the game. Faint specks away from the body are dropped.
"""
import os, sys, pathlib
import numpy as np
from PIL import Image, ImageFilter

COMFY = pathlib.Path('S:/AI/ComfyUI_windows_portable/ComfyUI-Easy-Install/ComfyUI')
WEIGHTS = COMFY / 'models' / 'background_removal' / 'birefnet.safetensors'
SRC = pathlib.Path('S:/AI/Game/Game Assets/monsters/monster')
ROOT = pathlib.Path(__file__).resolve().parent.parent / 'Assets' / 'Resources' / 'AdamsHaven'

sys.argv = sys.argv[:1]
sys.path.insert(0, str(COMFY))
os.chdir(COMFY)
import torch
import comfy.bg_removal_model

model = comfy.bg_removal_model.load(str(WEIGHTS))
for name in os.environ.get('CUTOUT_IDS', '').split():
    src = Image.open(SRC / (name + '.png')).convert('RGB')
    image = torch.from_numpy(np.asarray(src, dtype=np.float32) / 255.0).unsqueeze(0)
    with torch.no_grad():
        mask = model.encode_image(image)[0].float().cpu().numpy()
    alpha = np.clip(mask, 0, 1) * 255
    solid = Image.fromarray((alpha > 128).astype(np.uint8) * 255).filter(ImageFilter.MaxFilter(25))
    alpha = np.where(np.asarray(solid) > 0, alpha, 0).astype(np.uint8)
    rgba = np.dstack([np.asarray(src), alpha])
    Image.fromarray(rgba, 'RGBA').save(ROOT / 'FieldModels' / (name + '.png'))
    src.save(ROOT / 'Enemies' / (name + '.png'))
    print('CUT', name, 'opaque', round(float((alpha > 128).mean()), 3), flush=True)
