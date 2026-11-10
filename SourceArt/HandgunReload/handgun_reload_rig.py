# Handgun empty-reload rig helpers: source capture, FK, two-bone arm IK with twist. Run first (exec into bpy.app.driver_namespace).
# Space: LPSP_Rig armature space, cm; forward -Y, up +Z, screen right -X.
import bpy, math
import numpy as np
from mathutils import Vector, Matrix, Quaternion, Euler

lp = bpy.data.objects['LPSP_Rig']
gun = bpy.data.objects['Gun_Rig']
SRC_ARMS = bpy.data.actions['SRC_Arms_ReloadEmpty']
SRC_GUN = bpy.data.actions['SRC_Gun_ReloadEmpty']
F0, F1 = 1, 83
scene = bpy.context.scene
BONES = lp.data.bones
ROOTS = [b for b in BONES if b.parent is None]


def rest_rel(b):
    return (b.parent.matrix_local.inverted() @ b.matrix_local) if b.parent else b.matrix_local.copy()


REST_REL = {b.name: rest_rel(b) for b in BONES}
REST_REL_INV = {k: v.inverted() for k, v in REST_REL.items()}


def fk(bases):
    out = {}

    def rec(b, pm):
        m = pm @ REST_REL[b.name] @ bases[b.name]
        out[b.name] = m
        for c in b.children:
            rec(c, m)

    for r in ROOTS:
        rec(r, Matrix.Identity(4))
    return out


def set_world(bases, M, name, target):
    b = BONES[name]
    pm = M[b.parent.name] if b.parent else Matrix.Identity(4)
    bases[name] = REST_REL_INV[name] @ pm.inverted() @ target


def frame3(v, hint):
    x = v.normalized()
    z = x.cross(hint).normalized()
    y = z.cross(x)
    return Matrix((x, y, z)).transposed()


def rot_of(m):
    return m.to_3x3().normalized()


def with_rot(m, r3):
    loc, _, sca = m.decompose()
    return Matrix.Translation(loc) @ r3.to_4x4() @ Matrix.Diagonal((sca.x, sca.y, sca.z, 1.0))


def apply_bases(bases):
    for pb in lp.pose.bones:
        pb.matrix_basis = bases[pb.name]
    bpy.context.view_layer.update()


def capture_source():
    lp.animation_data.action = SRC_ARMS
    gun.animation_data.action = SRC_GUN
    B, W = {}, {}
    for f in range(F0, F1 + 1):
        scene.frame_set(f)
        B[f] = {pb.name: pb.matrix_basis.copy() for pb in lp.pose.bones}
        W[f] = {pb.name: pb.matrix.copy() for pb in lp.pose.bones}
    return B, W


SRCB, SRCW = capture_source()
SRCM = {f: fk(SRCB[f]) for f in SRCB}  # FK of source bases, should equal SRCW


def arm_bend(M, side):
    """Unit elbow bend direction (perpendicular to shoulder-wrist line) of an FK result."""
    up, lo, ha = f'upperarm_{side}', f'lowerarm_{side}', f'hand_{side}'
    S, E, W = M[up].translation, M[lo].translation, M[ha].translation
    d = (W - S).normalized()
    e = (E - S)
    return (e - d * e.dot(d)).normalized()


TWIST_LAST = {}  # last forearm twist angle per side, used to unwrap the angle between frames


def solve_arm(bases, side, T, pole, ref_M):
    """Two-bone IK on `bases` (modified). T = hand target world matrix (rot+loc), pole = bend hint vector,
    ref_M = FK of the source pose used as the reference for bone lengths and the twist split. Returns reach shortfall (cm)."""
    up, lo, ha, tw = f'upperarm_{side}', f'lowerarm_{side}', f'hand_{side}', f'lowerarm_twist_01_{side}'
    M = fk(bases)
    S, E0, W0 = M[up].translation.copy(), M[lo].translation.copy(), M[ha].translation.copy()
    L1, L2 = (E0 - S).length, (W0 - E0).length
    d = T.translation - S
    D = d.length
    dn = d.normalized()
    miss = max(0.0, D - (L1 + L2) * 0.999)
    D = min(D, (L1 + L2) * 0.999)
    a = (L1 * L1 - L2 * L2 + D * D) / (2 * D)
    h = math.sqrt(max(L1 * L1 - a * a, 0.0))
    bend = (pole - dn * pole.dot(dn)).normalized()
    E, W = S + dn * a + bend * h, S + dn * D
    R = frame3(E - S, W - S) @ frame3(E0 - S, W0 - S).inverted()
    set_world(bases, M, up, with_rot(M[up], (R @ rot_of(M[up])).normalized()))
    M = fk(bases)
    cur = M[ha].translation - M[lo].translation
    Rq = cur.rotation_difference(W - M[lo].translation).to_matrix()
    set_world(bases, M, lo, with_rot(M[lo], (Rq @ rot_of(M[lo])).normalized()))
    M = fk(bases)
    set_world(bases, M, ha, with_rot(M[ha], rot_of(T)))
    M = fk(bases)
    # forearm twist: half of the extra hand roll (vs. the reference pose) about the forearm axis goes to the twist bone
    axis = (M[ha].translation - M[lo].translation).normalized()
    base_rel = rot_of(ref_M[lo]).inverted() @ rot_of(ref_M[ha])
    new_rel = rot_of(M[lo]).inverted() @ rot_of(M[ha])
    delta = (new_rel @ base_rel.inverted()).to_quaternion()
    axis_l = rot_of(M[lo]).inverted() @ axis
    twist = 2.0 * math.atan2(Vector(delta[1:]).dot(axis_l), delta[0])
    twist = math.atan2(math.sin(twist), math.cos(twist))
    if side in TWIST_LAST:
        twist += 2.0 * math.pi * round((TWIST_LAST[side] - twist) / (2.0 * math.pi))
    TWIST_LAST[side] = twist
    q = Quaternion(axis, twist * 0.5).to_matrix()
    set_world(bases, M, tw, with_rot(M[tw], (q @ rot_of(M[tw])).normalized()))
    return miss


FINGERS = ('index', 'middle', 'ring', 'pinky')


def curl_fingers(bases, side, curls, thumb=None):
    """curls: dict finger -> (c1,c2,c3) degrees about the knuckle axis (index->pinky), applied in world space on top of `bases`."""
    for f in FINGERS:
        c = curls[f]
        for i in (1, 2, 3):
            n = f'{f}_0{i}_{side}'
            M = fk(bases)
            axis = (M[f'index_01_{side}'].translation - M[f'pinky_01_{side}'].translation).normalized()
            sign = 1.0 if side == 'r' else -1.0
            q = Quaternion(axis, math.radians(c[i - 1]) * sign).to_matrix()
            set_world(bases, M, n, with_rot(M[n], (q @ rot_of(M[n])).normalized()))


print('rig ready; source frames', len(SRCB), 'fk err', max((SRCM[34][n].translation - SRCW[34][n].translation).length for n in SRCM[34]))
