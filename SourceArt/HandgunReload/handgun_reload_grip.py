# Left-hand slide grip: circle-fit tube frame of the curled fingers, and a mesh penetration check against the pistol.
# Run after handgun_reload_rig.py in the same namespace.
import bmesh
from mathutils.bvhtree import BVHTree

arm_obj = bpy.data.objects['SK_Miliary_FPS_Arms']
gun_mesh = bpy.data.objects['SM_Handgun_03']
LFING = [f'{f}_0{i}_l' for f in FINGERS for i in (1, 2, 3)]
THUMB = [f'thumb_0{i}_l' for i in (1, 2, 3)]


def circle_center(points, z_axis):
    p0 = points[0]
    u = z_axis.orthogonal().normalized()
    v = z_axis.cross(u).normalized()
    xy = np.array([[(p - p0).dot(u), (p - p0).dot(v)] for p in points])
    a = np.c_[2 * xy, np.ones(len(xy))]
    b = (xy ** 2).sum(axis=1)
    s = np.linalg.lstsq(a, b, rcond=None)[0]
    r = math.sqrt(max(s[2] + s[0] ** 2 + s[1] ** 2, 0.0))
    return p0 + u * float(s[0]) + v * float(s[1]), r


def hand_tube(M, side, zsign=1.0):
    idx, pky = M[f'index_01_{side}'].translation, M[f'pinky_01_{side}'].translation
    z = (idx - pky).normalized() * zsign
    pts = [M[f'middle_0{i}_{side}'].translation.copy() for i in (1, 2, 3)]
    tip = M[f'middle_03_{side}'].translation + (M[f'middle_03_{side}'].translation - M[f'middle_02_{side}'].translation) * 0.8
    pts.append(tip)
    c, r = circle_center(pts, z)
    palm = (M[f'hand_{side}'].translation + M[f'middle_01_{side}'].translation) * 0.5
    x = palm - c
    x = (x - z * x.dot(z)).normalized()
    y = z.cross(x)
    F = Matrix((x, y, z)).transposed().to_4x4()
    F.translation = c
    return F, r


def open_left(base):
    b = {k: v.copy() for k, v in base.items()}
    for n in LFING + THUMB:
        b[n] = Matrix.Identity(4)
    return b


def tube_for(curl, base=None):
    """Left hand, flat fingers curled by `curl` degrees per joint. Returns (bases, tube frame F, radius, FK)."""
    b = open_left(base or SRCB[34])
    curl_fingers(b, 'l', {f: (curl,) * 3 for f in FINGERS})
    M = fk(b)
    F, r = hand_tube(M, 'l')
    return b, F, r, M


def tune_tube(target_r):
    best = None
    for deg in range(-110, 111, 2):
        b, F, r, M = tube_for(float(deg))
        # the fingertip must be on the palm side (+Y of the hand) of the knuckle
        H = M['hand_l'].inverted()
        if (H @ M['middle_03_l'].translation).y <= (H @ M['middle_01_l'].translation).y:
            continue
        s = abs(r - target_r)
        if best is None or s < best[0]:
            best = (s, deg, r)
    return best


_gun_bvh = {}


def gun_bvh_state():
    """BVH of the pistol in gun space at the current frame (evaluated, so slide / hammer pose included)."""
    dg = bpy.context.evaluated_depsgraph_get()
    ev = gun_mesh.evaluated_get(dg)
    me = ev.to_mesh()
    mw = gun.matrix_world.inverted() @ gun_mesh.matrix_world
    verts = [mw @ v.co for v in me.vertices]
    me.calc_loop_triangles()
    tris = [tuple(t.vertices) for t in me.loop_triangles]
    bv = BVHTree.FromPolygons(verts, tris)
    ev.to_mesh_clear()
    return bv


def left_vertex_ids():
    names = set(LFING + THUMB + ['hand_l'])
    gi = {vg.index for vg in arm_obj.vertex_groups if vg.name in names}
    out = {}
    for v in arm_obj.data.vertices:
        for g in v.groups:
            if g.group in gi and g.weight > 0.35:
                out[v.index] = arm_obj.vertex_groups[g.group].name
                break
    return out


def penetration(bv, G, groups=None, margin=0.0, rbv=None):
    """Return dict group-name -> penetration depth (cm, >0 means inside the pistol) of the left-hand skin, plus the count of inside
    vertices. G = gun matrix in armature space at the current frame. The depth is the second deepest vertex of the group, so one
    stray vertex next to an open mesh boundary does not count. With rbv (right hand BVH in gun space) the key 'RIGHT' holds the
    depth of the left hand inside the right hand (third deepest vertex)."""
    dg = bpy.context.evaluated_depsgraph_get()
    ev = arm_obj.evaluated_get(dg)
    me = ev.to_mesh()
    mw = G.inverted() @ lp.matrix_world.inverted() @ arm_obj.matrix_world
    per = {}
    rdeps = []
    n_in = 0
    for vi, gname in LEFT_IDS.items():
        p = mw @ me.vertices[vi].co
        loc, nor, idx, dist = bv.find_nearest(p, 3.0)
        if loc is not None:
            depth = dist if (p - loc).dot(nor) < 0.0 else -dist
            if depth > margin:
                n_in += 1
            per.setdefault(gname, []).append(depth)
        if rbv is not None:
            rl, rn, ri, rd = rbv.find_nearest(p, 3.0)
            if rl is not None:
                rdeps.append(rd if (p - rl).dot(rn) < 0.0 else -rd)
    ev.to_mesh_clear()
    res = {}
    for g, ds in per.items():
        ds.sort(reverse=True)
        res[g] = ds[1] if len(ds) > 1 else ds[0]
    if rbv is not None and rdeps:
        rdeps.sort(reverse=True)
        res['RIGHT'] = rdeps[min(2, len(rdeps) - 1)]
    return res, n_in


LEFT_IDS = left_vertex_ids()
print('left hand skin verts', len(LEFT_IDS))


# ---------------------------------------------------------------- grip fitting (offline helper, results are pasted into the keys file)
def fingers_curl_bases(base, curl_by_finger, thumb=None):
    b = open_left(base)
    curl_fingers(b, 'l', {f: (curl_by_finger[f],) * 3 for f in FINGERS})
    if thumb is not None:
        for n, m in thumb.items():
            b[n] = m
    return b


def fit_eval(Fg_c, a_deg, radius, G, base, pole, levels, thumb=None, ref_M=None, keep=False):
    """Place the left hand with its curl tube on the slide axis and measure, per finger, the penetration for a sweep of curl levels.
    Returns (tube data, {level: {group: depth}})."""
    best = tune_tube(radius)
    deg = best[1]
    b0, F, r, M0 = tube_for(float(deg))
    FT = F.inverted() @ M0['middle_03_l'].translation
    zs = -1.0 if FT.y > 0 else 1.0
    a = math.radians(a_deg)
    x = Vector((math.sin(a), 0.0, math.cos(a)))
    z = Vector((0.0, zs, 0.0))
    y = z.cross(x)
    Fg = Matrix((x, y, z)).transposed().to_4x4()
    Fg.translation = Fg_c
    Hrel = Fg @ (F.inverted() @ M0['hand_l'])
    T = G @ Hrel
    out = {}
    for t in levels:
        b = {k: v.copy() for k, v in SRCB[34].items()}
        ref = fk(b)
        Mb = fk(b)
        set_world(b, Mb, 'ik_hand_gun', G)
        solve_arm(b, 'l', T, pole, ref)
        fb = fingers_curl_bases(SRCB[34], {f: deg * t for f in FINGERS}, thumb)
        for n in LFING + THUMB:
            b[n] = fb[n]
        apply_bases(b)
        res, n_in = penetration(GUNBV, G, rbv=RBV if 'RBV' in globals() else None)
        out[t] = res
    return dict(deg=deg, Hrel=Hrel, Fg=Fg, radius=r, zs=zs), out


def score_fit(out, tol=0.12):
    """Per finger: largest curl level whose depth <= tol; cost = gap^2 there. Also palm penetration."""
    cost = 0.0
    chosen = {}
    levels = sorted(out)
    for f in FINGERS:
        names = [f'{f}_0{i}_l' for i in (1, 2, 3)]
        t_sel, gap = None, None
        for t in levels:
            d = max(out[t].get(n, -9.0) for n in names)
            if d <= tol:
                t_sel, gap = t, -d
        if t_sel is None:
            d0 = max(out[levels[0]].get(n, -9.0) for n in names)
            cost += 25.0 + d0 * d0
            chosen[f] = (None, d0)
        else:
            cost += max(0.0, gap) ** 2
            chosen[f] = (t_sel, gap)
    palm = max(out[levels[0]].get('hand_l', -9.0), out[levels[-1]].get('hand_l', -9.0))
    rr = max(out[t].get('RIGHT', -9.0) for t in levels if t >= 0.6)
    cost += 4.0 * max(0.0, rr - 0.3) ** 2
    cost += max(0.0, palm - 0.2) ** 2 * 4.0
    return cost, chosen


RIGHT_IDS = {v.index for v in arm_obj.data.vertices if any(arm_obj.vertex_groups[g.group].name.endswith('_r') and g.weight > 0.5
                                                           and any(k in arm_obj.vertex_groups[g.group].name for k in ('hand', 'index', 'middle', 'ring', 'pinky', 'thumb', 'lowerarm'))
                                                           for g in v.groups)}


def right_hand_bvh(G):
    """BVH (gun space) of the right hand / forearm skin in the currently applied pose."""
    ev = arm_obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    me = ev.to_mesh()
    me.calc_loop_triangles()
    mw = G.inverted() @ lp.matrix_world.inverted() @ arm_obj.matrix_world
    co = [mw @ v.co for v in me.vertices]
    tris = [tuple(t.vertices) for t in me.loop_triangles if all(i in RIGHT_IDS for i in t.vertices)]
    bv = BVHTree.FromPolygons(co, tris)
    ev.to_mesh_clear()
    return bv
