"""Character reference PNG -> high-resolution textured GLB with local ComfyUI (Pixal3D on the native Trellis.2 nodes).

  python Tools/char_to_3d.py Elara            # one character
  python Tools/char_to_3d.py all
  python Tools/char_to_3d.py Elara --quick    # cheaper settings for testing the pipeline
  python Tools/char_to_3d.py Elara --multiview  # front + Qwen left/back/right views (run char_views.py first)

Maximum-quality settings for a 16 GB card: structure decoded at 64^3, shape upsampled to 2048^3, MoGe-3 field of view,
more sampler steps, 1024^3 remesh, 4096 px base colour / normal / AO bakes. Time is not a constraint.
Outputs (per character, next to the source art): <Name>/pixal-max-v1/<name>_pixal_raw.glb
"""
from pathlib import Path
import json, shutil, sys, time, urllib.request
import numpy as np
from PIL import Image

CHARS = Path('S:/AI/Game/Game Assets/characters')
COMFY = Path('S:/AI/ComfyUI_windows_portable/ComfyUI-Easy-Install/ComfyUI')
URL = 'http://127.0.0.1:8188'

# character key -> (folder, reference png). The unnamed exec-*.png files were matched by eye / proportions.
REFS = {
    'Kaela': ('Kaela', 'kaela_battle_tpose_front_v6.png'),
    'Ghislaine': ('Ghislaine', 'Ghislaine (2).png'),
    'Elara': ('Elara', 'elara_battle_tpose_book_quill_v12.png'),
    'Daisy': ('Daisy', 'daisy-bonfire-wildheart-short-skirt-v7.png'),
    'Helda': ('Helda', 'exec-09495746-06b2-44ce-8fcf-12e72dc9a306.png'),
    'Clarity': ('Clarity', 'exec-912aa06f-476a-47dd-abf8-aa6049150ce0.png'),
    'CelestiumShort': ('Celestium', 'exec-212f0314-0e94-4446-8f3e-9d58d9aadb4a.png'),
    'CelestiumMuscle': ('Celestium', 'exec-2d52e50c-5ba7-4589-955a-0d766f15f992.png'),
    'CelestiumMed': ('Celestium', 'exec-ae2e53b3-a344-4d1f-9ed4-cc342366592e.png'),
}

MAX = dict(struct_res='64', struct_steps=25, shape_steps=30, up_res=2048, up_steps=20, tex_steps=20,
           remesh=1024, faces=400000, atlas=4096, moge_refine=6)
QUICK = dict(struct_res='32', struct_steps=12, shape_steps=20, up_res=1024, up_steps=12, tex_steps=12,
             remesh=640, faces=120000, atlas=2048, moge_refine=3)


def prepare(key):
    """Copy the reference into ComfyUI's input dir. Images with no real transparency get a BiRefNet mask instead."""
    folder, name = REFS[key]
    src = CHARS / folder / name
    im = Image.open(src)
    has_alpha = im.mode == 'RGBA' and (np.array(im)[:, :, 3] < 10).mean() > 0.05
    dst = COMFY / 'input' / f'char_{key}.png'
    im.convert('RGBA' if has_alpha else 'RGB').save(dst)
    return dst.name, has_alpha


def graph(image, has_alpha, prefix, s, seed=42, views=None):
    g = {}
    def n(i, cls, **inputs):
        g[str(i)] = {'class_type': cls, 'inputs': inputs}
    L = lambda i, o=0: [str(i), o]
    n(1, 'LoadImage', image=image)
    if has_alpha:
        n(2, 'InvertMask', mask=L(1, 1))
    else:
        n(90, 'LoadBackgroundRemovalModel', bg_removal_name='birefnet.safetensors')
        n(2, 'RemoveBackground', bg_removal_model=L(90), image=L(1))
    n(3, 'ImageCropToMask', images=L(1), masks=L(2), width=1024, height=1024, pad_factor=1.1, grow_mask=0,
      background='#000000')
    n(4, 'CLIPVisionLoader', clip_name='dino_v3_L_naf_fp32.safetensors')
    if views:
        # multi-view: front (alpha mask) plus Qwen-generated left/back/right, each cropped like the workflow template
        n(90, 'LoadBackgroundRemovalModel', bg_removal_name='birefnet.safetensors') if has_alpha else None
        cond = {}
        cond['front'] = L(3)
        for i, (vname, vimg) in enumerate(views.items()):
            base = 100 + i * 10
            n(base, 'LoadImage', image=vimg)
            n(base + 1, 'RemoveBackground', bg_removal_model=L(90), image=L(base))
            n(base + 2, 'ImageCropToMask', images=L(base), masks=L(base + 1), width=1024, height=1024, pad_factor=1.1,
              grow_mask=0, background='#000000')
            cond[vname] = L(base + 2)
        n(5, 'Pixal3DMultiViewConditioning', clip_vision_model=L(4), fov=20.0, front=cond['front'], left=cond['left'],
          back=cond['back'], right=cond['right'])
        n(6, 'UNETLoader', unet_name='pixal3d_multiview_int8_convrot.safetensors', weight_dtype='default')
    else:
        # camera field of view from MoGe-3, then Pixal3D pixel-aligned conditioning
        n(50, 'LoadMoGeModel', model_name='moge_3_vitl_fp16.safetensors')
        n(51, 'MoGeInference', moge_model=L(50), image=L(3), resolution_level=9, fov_x_degrees=0, batch_size=1,
          force_projection=True, apply_mask=True, refine_steps=s['moge_refine'])
        n(52, 'MoGeGeometryToFOV', moge_geometry=L(51), axis='horizontal', unit='degrees')
        n(5, 'Pixal3DConditioning', clip_vision_model=L(4), image=L(3), camera_angle_x=L(52))
        n(6, 'UNETLoader', unet_name='pixal3d_int8_convrot.safetensors', weight_dtype='default')
    n(7, 'VAELoader', vae_name='trellis_2_shape_vae_bf16.safetensors')
    n(8, 'VAELoader', vae_name='trellis_2_texture_vae_bf16.safetensors')
    # sparse structure
    n(10, 'CFGOverride', model=L(6), cfg=1, start_percent=0.667, end_percent=1)
    n(11, 'RescaleCFG', model=L(10), multiplier=0.7)
    n(12, 'ModelSamplingSD3', model=L(11), shift=5)
    n(13, 'EmptyTrellis2LatentStructure', batch_size=1)
    n(14, 'KSampler', model=L(12), seed=seed, control_after_generate='fixed', steps=s['struct_steps'], cfg=7.5,
      sampler_name='euler', scheduler='normal', positive=L(5, 0), negative=L(5, 1), latent_image=L(13), denoise=1)
    n(15, 'VaeDecodeStructureTrellis2', samples=L(14), vae=L(7), resolution=s['struct_res'])
    # shape, then upsample
    n(20, 'CFGOverride', model=L(6), cfg=1, start_percent=0.769, end_percent=1)
    n(21, 'RescaleCFG', model=L(20), multiplier=0.5)
    n(22, 'Trellis2ShapeStage', positive=L(5, 0), negative=L(5, 1), voxel=L(15))
    n(23, 'KSampler', model=L(21), seed=seed, control_after_generate='fixed', steps=s['shape_steps'], cfg=7.5,
      sampler_name='euler', scheduler='normal', positive=L(22, 0), negative=L(22, 1), latent_image=L(22, 2), denoise=1)
    n(24, 'Trellis2UpsampleStage', positive=L(22, 0), negative=L(22, 1), shape_latent=L(23), vae=L(7),
      target_resolution=s['up_res'])
    n(25, 'KSampler', model=L(21), seed=seed, control_after_generate='fixed', steps=s['up_steps'], cfg=7.5,
      sampler_name='euler', scheduler='simple', positive=L(24, 0), negative=L(24, 1), latent_image=L(24, 2), denoise=1)
    n(26, 'VaeDecodeShapeTrellis', samples=L(25), vae=L(7))
    # texture voxels
    n(30, 'Trellis2TextureStage', positive=L(24, 0), negative=L(24, 1), shape_latent=L(25))
    n(31, 'KSampler', model=L(21), seed=seed, control_after_generate='fixed', steps=s['tex_steps'], cfg=1,
      sampler_name='euler', scheduler='normal', positive=L(30, 0), negative=L(30, 1), latent_image=L(30, 2), denoise=1)
    n(32, 'VaeDecodeTextureTrellis', samples=L(31), vae=L(8), shape_subdivides=L(26, 1))
    # game mesh + baked maps
    g['39'] = {'class_type': 'RemeshMesh', 'inputs': {'mesh': L(26), 'resolution': s['remesh'], 'sign_mode': 'udf',
               'sign_mode.qef': False, 'sign_mode.drop_inverted_components': False,
               'sign_mode.drop_enclosed_components': False, 'band': 1.0, 'project_back': 0.0, 'fix_poles': False,
               'smooth_iters': 1, 'drop_small_components': 0.01, 'precluster_max_verts': 20000000}}
    n(40, 'DecimateMesh', mesh=L(39), target_face_count=s['faces'], placement_mode='midpoint')
    n(41, 'MeshSmoothNormals', mesh=L(40), crease_angle=180)
    n(42, 'UnwrapMesh', mesh=L(41), segmenter='pec', resolution=s['atlas'], padding=2, weld_distance=0.0002)
    n(43, 'BakeTextureFromVoxel', mesh=L(42), voxel_colors=L(32), texture_size=s['atlas'], reference_mesh=L(39))
    n(44, 'BakeAmbientOcclusion', low_poly=L(42), high_poly=L(39), resolution=2048, samples=128, max_distance=0.71,
      strength=1, bias=0.01)
    n(45, 'BakeNormalMapFromMesh', low_poly=L(42), high_poly=L(39), resolution=s['atlas'], cage_distance=0.05,
      ignore_backfaces=True)
    n(46, 'ApplyTextureToMesh', mesh=L(42), base_color=L(43, 0), metallic=L(43, 1), roughness=L(43, 2),
      occlusion=L(44), normal_map=L(45))
    n(47, 'MeshSmoothNormals', mesh=L(46), crease_angle=180)
    n(48, 'SaveGLB', mesh=L(47), filename_prefix=prefix)
    return g


def call(path, data=None):
    req = urllib.request.Request(URL + path, data=json.dumps(data).encode() if data is not None else None,
                                 headers={'Content-Type': 'application/json'})
    return json.load(urllib.request.urlopen(req, timeout=60))


def run(key, settings, tag, multiview=False):
    folder, _ = REFS[key]
    out = CHARS / folder / 'pixal-max-v1'
    out.mkdir(exist_ok=True)
    final = out / f'{key}_pixal_raw{tag}.glb'
    if final.exists():
        print(key, 'already generated'); return
    image, has_alpha = prepare(key)
    views = None
    if multiview:
        vd = out / 'views'
        views = {}
        for v in ('left', 'back', 'right'):
            shutil.copy(vd / f'{v}.png', COMFY / 'input' / f'view_{key}_{v}.png'); views[v] = f'view_{key}_{v}.png'
    pid = call('/prompt', {'prompt': graph(image, has_alpha, f'3d/char_{key}{tag}', settings, views=views)})['prompt_id']
    print(key, 'queued', pid, 'alpha' if has_alpha else 'birefnet', flush=True)
    t0 = time.time()
    while True:
        try:
            h = call('/history/' + pid).get(pid)
        except Exception as e:                       # ComfyUI busy or restarting: keep waiting
            print('poll error', e, flush=True); time.sleep(30); continue
        if h and h.get('status', {}).get('completed') is not None and (
                h['status']['completed'] or h['status'].get('status_str') == 'error'):
            break
        time.sleep(10)
    if h['status'].get('status_str') == 'error':
        print(json.dumps(h['status'], indent=1)[:3000]); raise SystemExit(f'{key} failed')
    files = [f for o in h['outputs'].values() for v in o.values() if isinstance(v, list) for f in v
             if isinstance(f, dict) and str(f.get('filename', '')).endswith('.glb')]
    print(key, 'done in %ds' % (time.time() - t0), files, flush=True)
    f = files[-1]
    shutil.copy(COMFY / 'output' / f.get('subfolder', '') / f['filename'], final)
    shutil.copy(COMFY / 'input' / image, out / f'{key}_reference.png')


if __name__ == '__main__':
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    quick, mv = '--quick' in sys.argv, '--multiview' in sys.argv
    settings, tag = (QUICK, '_quick') if quick else (MAX, '')
    tag += '_mv' if mv else ''
    for k in (list(REFS) if args in (['all'], []) else args):
        run(k, settings, tag, mv)
