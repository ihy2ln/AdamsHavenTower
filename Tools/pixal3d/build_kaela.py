"""Build the Pixal3D API prompt for a character: python build_kaela.py <image name in ComfyUI/input> <prefix> <faces>"""
import json, subprocess, sys
img, prefix, faces = sys.argv[1], sys.argv[2], int(sys.argv[3])
subprocess.check_call([sys.executable, 'ui2api.py', '3d_pixal3d_trellis2_image_to_model.json', 'api_raw.json'])
a = json.load(open('api_raw.json'))
a['122']['inputs']['image'] = img
a['312']['inputs']['background'] = '#000000'
a['56']['inputs']['refine_steps'] = 0
a['316']['inputs']['value'] = False                       # False = Pixal3D
a['241']['inputs'] = {'mesh': ['202', 0], 'resolution': 768, 'sign_mode': 'udf', 'sign_mode.qef': False,
    'sign_mode.drop_inverted_components': False, 'sign_mode.drop_enclosed_components': False, 'band': 1.0,
    'project_back': 0.0, 'fix_poles': False, 'smooth_iters': 20, 'drop_small_components': 0.01, 'precluster_max_verts': 20000000}
a['186']['inputs'] = {'mesh': ['241', 0], 'target_face_count': faces, 'placement_mode': 'midpoint'}
a['save'] = {'class_type': 'SaveGLB', 'inputs': {'mesh': ['285', 0], 'filename_prefix': prefix}}
for k in ('247', '282'):                                   # drop unused debug exports
    a.pop(k, None)
json.dump(a, open('api_kaela.json', 'w'), indent=1)
