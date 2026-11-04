"""Antscan step 1: a micro-CT volume of a real ant (Antscan, CC-BY 4.0) to the surface of its rigid body, in parts.

Run (with the scientific Python in D:/Tools/antscan-venv):
  python -I tools/antscan_mesh.py <tomogram.tif> <out-dir> [--step N] [--shell T]
The specimens are ethanol-preserved in a tube, often curled. The cuticle is a thin bright shell; the tissue inside is
about as dark as the ethanol around it. So:
  1. the shell is the voxels brighter than T (default: halfway from the ethanol's median to the 99.7th percentile),
     with the tube wall (a ring at the edge of every slice) masked off;
  2. the shell is closed and filled to a solid, and the largest piece kept: the ant;
  3. a morphological opening takes off the legs and antennae (far thinner than the body), and a second, stronger one
     parts the body at its two constrictions, the neck and the waist, into its three masses;
  4. every body voxel joins its nearest mass, and each part's surface is cut from the original solid, so the
     cuticle's own sculpture survives.
Writes <out-dir>/{head,mesosoma,gaster}.ply (voxel units) and parts.json: per part its centroid and principal axes,
the neck and waist points, the side the legs are on (down), and the voxel size, for tools/blender/antscan_body.py.
"""
import json
import os
import sys

import numpy as np
import tifffile
from scipy import ndimage
from skimage import measure


def arg(name, default):
    return type(default)(sys.argv[sys.argv.index(name) + 1]) if name in sys.argv else default


def largest(mask, k=1):
    lab, n = ndimage.label(mask)
    if n == 0:
        return [mask]
    sizes = ndimage.sum(mask, lab, range(1, n + 1))
    order = np.argsort(sizes)[::-1][:k]
    return [lab == (1 + int(i)) for i in order]


def write_ply(path, verts, faces):
    with open(path, "wb") as fh:
        fh.write(f"ply\nformat binary_little_endian 1.0\nelement vertex {len(verts)}\nproperty float x\nproperty float y\n"
                 f"property float z\nelement face {len(faces)}\nproperty list uchar int vertex_indices\nend_header\n".encode())
        fh.write(verts.astype("<f4").tobytes())
        rec = np.empty(len(faces), dtype=[("n", "u1"), ("i", "<i4", 3)])
        rec["n"] = 3
        rec["i"] = faces
        fh.write(rec.tobytes())


def main():
    src, out = sys.argv[1], sys.argv[2]
    step = arg("--step", 2)
    os.makedirs(out, exist_ok=True)
    raw = tifffile.imread(src)
    vol = ndimage.gaussian_filter(raw[::step, ::step, ::step].astype(np.float32), 0.7)
    del raw
    med = float(np.median(vol))
    T = arg("--shell", med + 0.5 * (float(np.percentile(vol, 99.7)) - med))
    # the tube: a cylinder along the slice axis; keep well inside its wall
    zs, ys, xs = vol.shape
    yy, xx = np.mgrid[0:ys, 0:xs]
    inside = np.hypot(yy - ys / 2, xx - xs / 2) < 0.43 * min(ys, xs)
    shell = (vol > T) & inside[None]
    shell = ndimage.binary_closing(shell, ndimage.generate_binary_structure(3, 1), iterations=4)
    # the shell has gaps at the joints and the spiracles, so a 3D fill leaks: fill it slice by slice along all three
    # axes and keep what any of them encloses
    solid = shell.copy()
    for ax in range(3):
        solid |= np.moveaxis(np.stack([ndimage.binary_fill_holes(sl) for sl in np.moveaxis(shell, ax, 0)]), 0, ax)
    solid = ndimage.binary_fill_holes(solid)
    ant = largest(solid)[0]
    print(f"[antscan] volume {vol.shape} step {step}: ethanol {med:.0f}, shell threshold {T:.0f}, ant voxels {int(ant.sum())}")

    edt = ndimage.distance_transform_edt(ant)
    thick = float(np.percentile(edt[ant], 99.8))
    # the body without its limbs: open wider than a leg, grow back, keep the original solid inside
    # (an opening this wide may already part the head at a slender neck, so keep every sizeable piece)
    pieces = largest(edt > 0.3 * thick, 3)
    core = np.zeros_like(ant)
    for pc in pieces:
        if pc.sum() > 0.02 * ant.sum():
            core |= pc
    body = ant & (ndimage.distance_transform_edt(~core) <= 0.3 * thick * 1.3)
    print(f"[antscan] body: {len(pieces)} pieces after the limb opening, {int(body.sum())} voxels, thickness {thick:.1f}")
    # the three masses: open until the neck and the waist part them
    masses = []
    for f in np.arange(0.35, 0.8, 0.05):
        masses = largest(edt > f * thick, 3)
        if len(masses) == 3 and min(m.sum() for m in masses) > 0.01 * body.sum():
            break
    if len(masses) < 3:
        raise SystemExit("could not part the body into three masses")
    vols = [int(m.sum()) for m in masses]
    cents = [np.argwhere(m).mean(0) for m in masses]
    # the gaster is the biggest; the mesosoma lies between the other two; the head is the remaining one
    g = int(np.argmax(vols))
    others = [i for i in range(3) if i != g]
    d = lambda a, b: np.linalg.norm(cents[a] - cents[b])
    m = min(others, key=lambda i: d(i, g))
    h = [i for i in others if i != m][0]
    # every body voxel to its nearest mass
    dist = np.stack([ndimage.distance_transform_edt(~masses[i]) for i in (h, m, g)])
    owner = np.argmin(dist, axis=0)
    legs = ant & ~body
    info = {"step": step, "threshold": T, "thickness": thick, "parts": {}}
    for k, name in enumerate(("head", "mesosoma", "gaster")):
        part = body & (owner == k)
        part = largest(part)[0]
        print(f"[antscan] {name}: {int(part.sum())} voxels (mass {vols[(h, m, g)[k]]})")
        v, fcs, _, _ = measure.marching_cubes(np.pad(part, 1).astype(np.float32), 0.5, step_size=1)
        v = (v - 1) * step  # back to full-resolution voxels, (z, y, x)
        write_ply(os.path.join(out, name + ".ply"), v[:, ::-1], fcs)
        pts = np.argwhere(part).astype(np.float64)
        c = pts.mean(0)
        w, vecs = np.linalg.eigh(np.cov((pts - c).T))
        info["parts"][name] = {"centroid": (c[::-1] * step).tolist(), "axes": [vecs[::-1, j].tolist() for j in (2, 1, 0)],
                               "voxels": int(part.sum()), "triangles": len(fcs)}
        print(f"[antscan] {name}: {len(v)} vertices, {len(fcs)} triangles")
    # where the parts meet: the neck (head-mesosoma) and the waist (mesosoma-gaster)
    def joint(a, b):
        za = (owner == a) & body
        zb = ndimage.binary_dilation((owner == b) & body, iterations=2)
        p = np.argwhere(za & zb)
        return (p.mean(0)[::-1] * step).tolist() if len(p) else None
    info["neck"] = joint(0, 1)
    info["waist"] = joint(1, 2)
    lp = np.argwhere(legs & (ndimage.distance_transform_edt(~((owner == 1) & body)) < 0.6 * thick))
    info["legs"] = (lp.mean(0)[::-1] * step).tolist() if len(lp) else None
    json.dump(info, open(os.path.join(out, "parts.json"), "w"), indent=1)


main()
