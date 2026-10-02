"""Turn a raw (open, overlapping-shell) Pixal3D mesh into one watertight closed surface. Run with ComfyUI's python:

  S:/AI/ComfyUI_windows_portable/ComfyUI-Easy-Install/python_embeded/python.exe Tools/char_close.py <raw.glb> <closed.glb> [pitch_mm]

Voxelises every triangle (so open cloth/hair sheets count), thickens by one voxel, closes gaps, fills the interior and
extracts a smooth marching-cubes surface. Output stays in the raw mesh's coordinates so it can be matched 1:1 with it.
"""
import sys
import numpy as np
import trimesh
from scipy import ndimage as ndi
from skimage import measure

src, dst = sys.argv[1:3]
pitch_mm = float(sys.argv[3]) if len(sys.argv) > 3 else 2.2
scene = trimesh.load(src, force='scene')
mesh = trimesh.util.concatenate([g for g in scene.dump() if isinstance(g, trimesh.Trimesh)])
height = mesh.bounds[1][1] - mesh.bounds[0][1]                      # glTF is Y-up
pitch = height / 1800.0 * (pitch_mm / 2.2) * 1.0
print('raw', len(mesh.faces), 'faces, height', round(height, 3), 'pitch', pitch, flush=True)

# dense surface samples -> occupied voxels (sampling density well above one point per voxel face)
lo = mesh.bounds[0] - pitch * 6
dims = np.ceil((mesh.bounds[1] + pitch * 6 - lo) / pitch).astype(int)
print('grid', dims, flush=True)
occ = np.zeros(dims, bool)
area = mesh.area
n = int(area / (pitch * pitch) * 6)
for start in range(0, n, 4_000_000):
    pts, _ = trimesh.sample.sample_surface(mesh, min(4_000_000, n - start))
    idx = np.floor((pts - lo) / pitch).astype(int)
    occ[idx[:, 0], idx[:, 1], idx[:, 2]] = True
print('surface voxels', int(occ.sum()), flush=True)
occ = ndi.binary_dilation(occ, iterations=1)
occ = ndi.binary_closing(occ, structure=ndi.generate_binary_structure(3, 2), iterations=2)
solid = ndi.binary_fill_holes(occ)
field = ndi.gaussian_filter(solid.astype(np.float32), sigma=1.2)
verts, faces, normals, _ = measure.marching_cubes(field, level=0.5, spacing=(pitch, pitch, pitch))
verts += lo
out = trimesh.Trimesh(verts, faces, process=True)
print('closed', len(out.faces), 'faces, watertight', out.is_watertight, 'components', len(out.split(only_watertight=False)), flush=True)
out.export(dst)
