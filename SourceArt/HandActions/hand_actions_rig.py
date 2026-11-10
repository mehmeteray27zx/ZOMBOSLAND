# First-person hand action clips (pickup, lower/raise, container, bandage, craft). Shared rig helpers.
# Space: LPSP_Rig armature space, cm; forward -Y, up +Z, screen right -X. FBX 30 fps.
# Reuses the two-bone IK approach of SourceArt/HandgunReload/handgun_reload_rig.py but starts from the LPSP Idle pose.
import bpy, math
from mathutils import Vector, Matrix, Quaternion, Euler

lp = bpy.data.objects['LPSP_Rig']
gun = bpy.data.objects['Gun_Rig']
scene = bpy.context.scene
BONES = lp.data.bones
ROOTS = [b for b in BONES if b.parent is None]
SIDES = 'lr'
FINGERS = ('index', 'middle', 'ring', 'pinky')


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


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def mat_blend(A, B, t):
    q = A.to_quaternion().slerp(B.to_quaternion(), t)
    M = q.to_matrix().to_4x4()
    M.translation = A.translation.lerp(B.translation, t)
    return M


def bases_blend(bA, bB, t, names=None):
    out = {}
    for n in (names or bA.keys()):
        la, qa, sa = bA[n].decompose()
        lb, qb, sb = bB[n].decompose()
        if qa.dot(qb) < 0.0:
            qb = -qb
        q = qa.slerp(qb, t)
        out[n] = Matrix.Translation(la.lerp(lb, t)) @ q.to_matrix().to_4x4() @ Matrix.Diagonal((*sa.lerp(sb, t), 1.0))
    return out


def Rx(d):
    return Matrix.Rotation(math.radians(d), 4, 'X')


def Ry(d):
    return Matrix.Rotation(math.radians(d), 4, 'Y')


def Rz(d):
    return Matrix.Rotation(math.radians(d), 4, 'Z')


def source_bases(action, frame):
    lp.animation_data.action = action
    scene.frame_set(frame)
    return {pb.name: pb.matrix_basis.copy() for pb in lp.pose.bones}


IDLE_ACTION = bpy.data.actions['SRC_Idle_Pose']
IDLE = source_bases(IDLE_ACTION, 1)
IDLE_M = fk(IDLE)
LF = [f'{f}_0{i}_l' for f in FINGERS + ('thumb',) for i in (1, 2, 3)]
RF = [f'{f}_0{i}_r' for f in FINGERS + ('thumb',) for i in (1, 2, 3)]


def arm_bend(M, side):
    up, lo, ha = f'upperarm_{side}', f'lowerarm_{side}', f'hand_{side}'
    S, E, W = M[up].translation, M[lo].translation, M[ha].translation
    d = (W - S).normalized()
    e = (E - S)
    return (e - d * e.dot(d)).normalized()


TWIST_LAST = {}


def solve_arm(bases, side, T, pole, ref_M):
    """Two-bone IK. T = hand target world matrix, pole = elbow hint, ref_M = FK of the reference pose. Returns reach shortfall (cm)."""
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


def curl_bases(bases, side, curls, thumb_curl=0.0, spread=0.0):
    """Returns finger bases with each finger curled by curls[finger]=(c1,c2,c3) degrees from the rest (flat) pose."""
    out = dict(bases)
    for f in FINGERS:
        c = curls[f]
        for i in (1, 2, 3):
            n = f'{f}_0{i}_{side}'
            M = fk(out)
            axis = (M[f'index_01_{side}'].translation - M[f'pinky_01_{side}'].translation).normalized()
            sign = 1.0 if side == 'r' else -1.0
            q = Quaternion(axis, math.radians(c[i - 1]) * sign).to_matrix()
            set_world(out, M, n, with_rot(M[n], (q @ rot_of(M[n])).normalized()))
    return out


def key_all(frame, prev_q):
    for pb in lp.pose.bones:
        loc, q, sc = pb.matrix_basis.decompose()
        pq = prev_q.get(pb.name)
        if pq is not None and q.dot(pq) < 0.0:
            q = -q
        prev_q[pb.name] = q.copy()
        pb.location = loc
        pb.rotation_quaternion = q
        pb.scale = sc
        pb.keyframe_insert('location', frame=frame, group=pb.name)
        pb.keyframe_insert('rotation_quaternion', frame=frame, group=pb.name)
        pb.keyframe_insert('scale', frame=frame, group=pb.name)


def bake_clip(name, f0, f1, pose_fn):
    """pose_fn(frame) -> bases dict. Creates or replaces action `name`, one key per frame, linear."""
    old = bpy.data.actions.get(name)
    if old:
        bpy.data.actions.remove(old)
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    lp.animation_data.action = act
    TWIST_LAST.clear()
    prev = {}
    missed = {}
    for f in range(f0, f1 + 1):
        b, info = pose_fn(f)
        apply_bases(b)
        key_all(f, prev)
        if info:
            missed[f] = info
    for fc in act.fcurves:
        for k in fc.keyframe_points:
            k.interpolation = 'LINEAR'
    return act, missed


def set_gun_rest():
    """Pistol parts in their reference pose (slide forward)."""
    a = bpy.data.actions.get('GUN_Static')
    if a is None:
        # Frame 83 of the reload clip is the normal pose (slide forward); one key per bone holds it for every frame.
        gun.animation_data.action = bpy.data.actions['A_FP_WEP_Handgun_ReloadEmpty_Gun']
        scene.frame_set(83)
        bpy.context.view_layer.update()
        pose = {pb.name: pb.matrix_basis.copy() for pb in gun.pose.bones}
        a = bpy.data.actions.new('GUN_Static')
        a.use_fake_user = True
        gun.animation_data.action = a
        for pb in gun.pose.bones:
            pb.rotation_mode = 'QUATERNION'
            pb.matrix_basis = pose[pb.name]
            for prop in ('location', 'rotation_quaternion', 'scale'):
                pb.keyframe_insert(prop, frame=1, group=pb.name)
    gun.animation_data.action = a
    gun.location = (0.0, 0.0, 0.0)
    gun.rotation_euler = (0.0, 0.0, 0.0)
    gun.scale = (1.0, 1.0, 1.0)  # the source action left 0.01 on the object; the CHILD_OF constraint already places it in cm space
    bpy.context.view_layer.update()


print('hand action rig ready', len(LF), 'hand_l idle', [round(x, 2) for x in IDLE_M['hand_l'].translation], 'hand_r idle', [round(x, 2) for x in IDLE_M['hand_r'].translation],
      'gun', [round(x, 2) for x in IDLE_M['ik_hand_gun'].translation], 'shoulder_l', [round(x, 2) for x in IDLE_M['upperarm_l'].translation], 'shoulder_r', [round(x, 2) for x in IDLE_M['upperarm_r'].translation])
