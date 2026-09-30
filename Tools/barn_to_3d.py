"""Barn tier paintings -> textured GLB via local ComfyUI (native Trellis.2 nodes).

  python Tools/barn_to_3d.py F        # one tier
  python Tools/barn_to_3d.py all

Reads  S:/AI/.../Barn Game Ready/barn_<tier>.png  (copied to ComfyUI input as barn_<tier>.png)
Writes ComfyUI output 3d/barn_<tier>_*.glb, then copies to BarnModels/<tier>.glb (outside Assets).
"""
from pathlib import Path
import json, shutil, sys, time, urllib.request

PROJECT = Path(__file__).resolve().parent.parent
PACK = Path('S:/AI/Harness/Codex/home/generated_images/01a0f00a-6833-72c3-a8b8-4f6aef0e36b9/Barn F-SSR/Barn Game Ready')
COMFY = Path('S:/AI/ComfyUI_windows_portable/ComfyUI-Easy-Install/ComfyUI')
URL = 'http://127.0.0.1:8188'
TIERS = ['F', 'E', 'D', 'C', 'B', 'A', 'S', 'SS', 'SSR']
OUT = PROJECT / 'BarnModels'


def graph(image, prefix, seed=42, faces=120000, tex=2048):
    g = {}
    def n(i, cls, **inputs):
        g[str(i)] = {'class_type': cls, 'inputs': inputs}
        return [str(i)]
    L = lambda i, o=0: [str(i), o]
    n(1, 'LoadImage', image=image)
    n(2, 'InvertMask', mask=L(1, 1))
    n(3, 'ImageCropToMask', images=L(1), masks=L(2), width=1024, height=1024, pad_factor=1.0, grow_mask=0,
      background='#000000')
    n(4, 'CLIPVisionLoader', clip_name='dino_v3_L_naf_fp32.safetensors')
    n(5, 'Trellis2Conditioning', clip_vision_model=L(4), image=L(3))
    n(6, 'UNETLoader', unet_name='trellis_2_int8_convrot.safetensors', weight_dtype='default')
    n(7, 'VAELoader', vae_name='trellis_2_shape_vae_bf16.safetensors')
    n(8, 'VAELoader', vae_name='trellis_2_texture_vae_bf16.safetensors')
    # sparse structure
    n(10, 'CFGOverride', model=L(6), cfg=1, start_percent=0.667, end_percent=1)
    n(11, 'RescaleCFG', model=L(10), multiplier=0.7)
    n(12, 'ModelSamplingSD3', model=L(11), shift=5)
    n(13, 'EmptyTrellis2LatentStructure', batch_size=1)
    n(14, 'KSampler', model=L(12), seed=seed, control_after_generate='fixed', steps=12, cfg=7.5, sampler_name='euler',
      scheduler='normal', positive=L(5, 0), negative=L(5, 1), latent_image=L(13), denoise=1)
    n(15, 'VaeDecodeStructureTrellis2', samples=L(14), vae=L(7), resolution='32')
    # shape
    n(20, 'CFGOverride', model=L(6), cfg=1, start_percent=0.769, end_percent=1)
    n(21, 'RescaleCFG', model=L(20), multiplier=0.5)
    n(22, 'Trellis2ShapeStage', positive=L(5, 0), negative=L(5, 1), voxel=L(15))
    n(23, 'KSampler', model=L(21), seed=seed, control_after_generate='fixed', steps=20, cfg=7.5, sampler_name='euler',
      scheduler='normal', positive=L(22, 0), negative=L(22, 1), latent_image=L(22, 2), denoise=1)
    n(24, 'Trellis2UpsampleStage', positive=L(22, 0), negative=L(22, 1), shape_latent=L(23), vae=L(7),
      target_resolution=1536)
    n(25, 'KSampler', model=L(21), seed=seed, control_after_generate='fixed', steps=12, cfg=7.5, sampler_name='euler',
      scheduler='simple', positive=L(24, 0), negative=L(24, 1), latent_image=L(24, 2), denoise=1)
    n(26, 'VaeDecodeShapeTrellis', samples=L(25), vae=L(7))
    # texture
    n(30, 'Trellis2TextureStage', positive=L(24, 0), negative=L(24, 1), shape_latent=L(25))
    n(31, 'KSampler', model=L(21), seed=seed, control_after_generate='fixed', steps=12, cfg=1, sampler_name='euler',
      scheduler='normal', positive=L(30, 0), negative=L(30, 1), latent_image=L(30, 2), denoise=1)
    n(32, 'VaeDecodeTextureTrellis', samples=L(31), vae=L(8), shape_subdivides=L(26, 1))
    # game mesh + baked maps
    g['39'] = {'class_type': 'RemeshMesh', 'inputs': {'mesh': L(26), 'resolution': 640, 'sign_mode': 'udf',
               'sign_mode.qef': False, 'sign_mode.drop_inverted_components': False,
               'sign_mode.drop_enclosed_components': False, 'band': 1.0, 'project_back': 0.0, 'fix_poles': False,
               'smooth_iters': 1, 'drop_small_components': 0.01, 'precluster_max_verts': 20000000}}
    n(40, 'DecimateMesh', mesh=L(39), target_face_count=faces, placement_mode='midpoint')
    n(41, 'MeshSmoothNormals', mesh=L(40), crease_angle=180)
    n(42, 'UnwrapMesh', mesh=L(41), segmenter='pec', resolution=tex, padding=1, weld_distance=0.0002)
    n(43, 'BakeTextureFromVoxel', mesh=L(42), voxel_colors=L(32), texture_size=tex, reference_mesh=L(39))
    n(44, 'BakeAmbientOcclusion', low_poly=L(42), high_poly=L(39), resolution=1024, samples=64, max_distance=0.71,
      strength=1, bias=0.01)
    n(45, 'BakeNormalMapFromMesh', low_poly=L(42), high_poly=L(39), resolution=tex, cage_distance=0.05,
      ignore_backfaces=True)
    n(46, 'ApplyTextureToMesh', mesh=L(42), base_color=L(43, 0), metallic=L(43, 1), roughness=L(43, 2),
      occlusion=L(44), normal_map=L(45))
    n(47, 'MeshSmoothNormals', mesh=L(46), crease_angle=180)
    n(48, 'SaveGLB', mesh=L(47), filename_prefix=prefix)
    return g


def call(path, data=None):
    req = urllib.request.Request(URL + path, data=json.dumps(data).encode() if data is not None else None,
                                 headers={'Content-Type': 'application/json'})
    return json.load(urllib.request.urlopen(req))


def run(tier):
    src = PACK / f'barn_{tier}.png'
    dst = COMFY / 'input' / f'barn_{tier}.png'
    shutil.copy(src, dst)
    prefix = f'3d/barn_{tier}'
    pid = call('/prompt', {'prompt': graph(dst.name, prefix)})['prompt_id']
    print(tier, 'queued', pid, flush=True)
    t0 = time.time()
    while True:
        h = call('/history/' + pid).get(pid)
        if h and h.get('status', {}).get('completed') is not None and (h['status']['completed'] or h['status'].get('status_str') == 'error'):
            break
        time.sleep(5)
    if h['status'].get('status_str') == 'error':
        print(json.dumps(h['status'], indent=1)[:3000]); raise SystemExit(1)
    files = [f for o in h['outputs'].values() for v in o.values() if isinstance(v, list) for f in v
             if isinstance(f, dict) and str(f.get('filename', '')).endswith('.glb')]
    print(tier, 'done in %ds' % (time.time() - t0), files, flush=True)
    OUT.mkdir(exist_ok=True)
    f = files[-1]
    shutil.copy(COMFY / 'output' / f.get('subfolder', '') / f['filename'], OUT / f'{tier}.glb')


if __name__ == '__main__':
    args = sys.argv[1:] or ['F']
    for t in (TIERS if args == ['all'] else args):
        if (OUT / f'{t}.glb').exists(): print(t, 'already generated'); continue
        run(t)
