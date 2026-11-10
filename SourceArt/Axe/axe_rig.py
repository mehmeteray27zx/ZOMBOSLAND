# Two-handed axe viewmodel posing on the LPSP skeleton. Space: rig armature space, cm; forward -Y, up +Z, right -X.
TARGET_R = 3.0  # finger centreline radius around the haft (haft half-width about 2 cm)
import bpy, math
import numpy as np
from mathutils import Vector, Matrix, Quaternion

lp = bpy.data.objects.get('LPSP_Rig') or bpy.data.objects['Armature']
axe = bpy.data.objects['wooden_axe_02']
SRC = bpy.data.actions['Armature|Scene']
SRC.use_fake_user = True


def capture_base():
    lp.animation_data_create()
    lp.animation_data.action = SRC
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    return {pb.name: pb.matrix_basis.copy() for pb in lp.pose.bones}


BASE = capture_base()
BONES = lp.data.bones
ROOTS = [b for b in BONES if b.parent is None]


def rest_rel(b):
    return (b.parent.matrix_local.inverted() @ b.matrix_local) if b.parent else b.matrix_local.copy()


def fk(bases):
    out = {}

    def rec(b, pm):
        m = pm @ rest_rel(b) @ bases[b.name]
        out[b.name] = m
        for c in b.children:
            rec(c, m)

    for r in ROOTS:
        rec(r, Matrix.Identity(4))
    return out


def set_world(bases, M, name, target):
    b = BONES[name]
    pm = M[b.parent.name] if b.parent else Matrix.Identity(4)
    bases[name] = rest_rel(b).inverted() @ pm.inverted() @ target


def frame3(v, hint):
    x = v.normalized()
    z = x.cross(hint).normalized()
    y = z.cross(x)
    m = Matrix((x, y, z)).transposed()
    return m


def rot_of(m):
    return m.to_3x3().normalized()


def with_rot(m, r3):
    loc, _, sca = m.decompose()
    return Matrix.Translation(loc) @ r3.to_4x4() @ Matrix.Diagonal((sca.x, sca.y, sca.z, 1.0))


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


def hand_tube(M, side):
    idx, pky = M[f'index_01_{side}'].translation, M[f'pinky_01_{side}'].translation
    z = (idx - pky).normalized()
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


# Grip stations on the axe (axe-local cm, axe Z = haft towards head, -Y = blade edge). angle = palm direction around the haft.
GRIP = {'r': (Vector((0.0, -0.4, 3.0)), 20.0), 'l': (Vector((0.0, 1.75, -15.0)), -20.0)}
POLE = {'r': Vector((-0.75, 0.15, -1.0)), 'l': Vector((0.8, 0.15, -1.0))}


def axe_on_haft(side):
    c, ang = GRIP[side]
    a = math.radians(ang)
    x = Vector((math.sin(a), math.cos(a), 0.0))  # 0 deg: palm behind the haft, opposite the blade
    z = Vector((0.0, 0.0, 1.0))
    y = z.cross(x)
    F = Matrix((x, y, z)).transposed().to_4x4()
    F.translation = c
    return F


def axe_matrix(grip_pos, haft_dir, blade_hint):
    z = haft_dir.normalized()
    blade = (blade_hint - z * blade_hint.dot(z)).normalized()
    y = -blade
    x = y.cross(z)
    A = Matrix((x, y, z)).transposed().to_4x4()
    A.translation = grip_pos - A.to_3x3() @ GRIP['r'][0]
    return A


def solve_arm(bases, side, T):
    up, lo, ha, tw = f'upperarm_{side}', f'lowerarm_{side}', f'hand_{side}', f'lowerarm_twist_01_{side}'
    M = fk(bases)
    S, E0, W0 = M[up].translation.copy(), M[lo].translation.copy(), M[ha].translation.copy()
    L1, L2 = (E0 - S).length, (W0 - E0).length
    d = T.translation - S
    D = d.length
    dn = d.normalized()
    miss = max(0.0, D - (L1 + L2) * 0.998)
    D = min(D, (L1 + L2) * 0.998)
    a = (L1 * L1 - L2 * L2 + D * D) / (2 * D)
    h = math.sqrt(max(L1 * L1 - a * a, 0.0))
    pole = POLE[side]
    bend = (pole - dn * pole.dot(dn)).normalized()
    E, W = S + dn * a + bend * h, S + dn * D
    R = frame3(E - S, W - S) @ frame3(E0 - S, W0 - S).inverted()
    set_world(bases, M, up, with_rot(M[up], (R @ rot_of(M[up])).normalized()))
    M = fk(bases)
    cur = M[ha].translation - M[lo].translation
    Rq = cur.rotation_difference(W - M[lo].translation).to_matrix()
    set_world(bases, M, lo, with_rot(M[lo], (Rq @ rot_of(M[lo])).normalized()))
    M = fk(bases)
    # hand orientation, keeping the base scale
    set_world(bases, M, ha, with_rot(M[ha], rot_of(T)))
    M = fk(bases)
    # forearm twist: half of the hand's roll about the forearm goes to the twist bone
    axis = (M[ha].translation - M[lo].translation).normalized()
    base_rel = rot_of(M0[lo]).inverted() @ rot_of(M0[ha])
    new_rel = rot_of(M[lo]).inverted() @ rot_of(M[ha])
    delta = (new_rel @ base_rel.inverted()).to_quaternion()
    axis_l = rot_of(M[lo]).inverted() @ axis
    twist = 2.0 * math.atan2(Vector(delta[1:]).dot(axis_l), delta[0])
    twist = math.atan2(math.sin(twist), math.cos(twist))
    q = Quaternion(axis, twist * 0.5).to_matrix()
    set_world(bases, M, tw, with_rot(M[tw], (q @ rot_of(M[tw])).normalized()))
    return miss


def curl_fingers(bases, side, curl_deg, thumb_deg=0.0):
    M = fk(bases)
    axis = (M[f'index_01_{side}'].translation - M[f'pinky_01_{side}'].translation).normalized()
    sign = 1.0 if side == 'r' else -1.0
    for f in ('index', 'middle', 'ring', 'pinky'):
        for i in (1, 2, 3):
            n = f'{f}_0{i}_{side}'
            M = fk(bases)
            q = Quaternion(axis, math.radians(curl_deg) * sign).to_matrix()
            set_world(bases, M, n, with_rot(M[n], (q @ rot_of(M[n])).normalized()))


def tune_curl(side, target_r):
    best = None
    for deg in range(-40, 41, 2):
        b = {k: v.copy() for k, v in BASE.items()}
        curl_fingers(b, side, float(deg))
        F, r = hand_tube(fk(b), side)
        score = abs(r - target_r)
        if best is None or score < best[0]:
            best = (score, deg, r)
    curl_fingers(BASE, side, float(best[1]))
    return best[1], best[2]


CURL = {s: tune_curl(s, TARGET_R) for s in 'rl'}
M0 = fk(BASE)
TUBE = {s: hand_tube(M0, s) for s in 'rl'}
HAND_IN_TUBE = {s: TUBE[s][0].inverted() @ M0[f'hand_{s}'] for s in 'rl'}


def pose_for(A, finger_r=0.0, finger_l=0.0, body=None):
    bases = {k: v.copy() for k, v in BASE.items()}
    misses = {}
    if body is not None:
        M = fk(bases)
        set_world(bases, M, 'root', Matrix.Translation(body) @ M['root'])
    for s in 'rl':
        T = A @ axe_on_haft(s) @ HAND_IN_TUBE[s]
        misses[s] = solve_arm(bases, s, T)
    if finger_r:
        curl_fingers(bases, 'r', finger_r)
    if finger_l:
        curl_fingers(bases, 'l', finger_l)
    return bases, misses


def apply_bases(bases):
    for pb in lp.pose.bones:
        pb.matrix_basis = bases[pb.name]
    bpy.context.view_layer.update()


def place_axe(A):
    axe.hide_viewport = False
    axe.hide_render = False
    axe.parent = None
    axe.matrix_world = A @ Matrix.Diagonal((100.0, 100.0, 100.0, 1.0))
    bpy.context.view_layer.update()


def show(A, finger_r=0.0, finger_l=0.0):
    lp.animation_data.action = None
    b, m = pose_for(A, finger_r, finger_l)
    apply_bases(b)
    place_axe(A)
    Mf = fk(b)
    return m, Mf
