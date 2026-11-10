# Handgun empty-reload "cocky rack": keys, pose solver and bake. Run after handgun_reload_rig.py and handgun_reload_grip.py.
# Replaces frames 35-72 of the source clip; frames 1-34 and 73-83 are copied from the source bases.

P = dict(
    # pistol keys (armature space, cm / degrees)
    cock_yaw=-30.0, cock_loc=Vector((-6.0, -32.0, -8.0)), cock_pitch=17.0, cock_roll=10.0, zs_flip=False,
    pull_off=Vector((-0.6, -0.8, 0.0)), pull_pitch=3.0,
    level_loc=Vector((-4.3, -33.0, -10.5)),
    kick_back=1.0, kick_pitch=3.0,
    # left hand slide grip (gun space)
    grip_x=Vector((0.0, 1.0, 0.0)), grip_z=Vector((0.0, 0.0, -1.0)), grip_c=Vector((-0.8, 17.0, 4.5)), grip_a=0.0, grip_radius=3.8,
    grip_t=dict(index=1.0, middle=0.9, ring=0.9, pinky=1.2), thumb_eul=[-18.0, -31.6, -14.1, 8.3, -11.2, 2.6, -3.9, 16.8, 24.9],
    grip_shift=Vector((0.0, 0.0, 0.0)),
    # left hand fly-off after the snap
    fly_dir=Vector((1.0, -0.5816, -0.4877)), fly_dist=8.352, out_dist=10.0, fly_rot=1.0, rot_start=47.0, hov_yaw=0.0, hov_pitch=0.0, hover_target=Vector((10.0, -25.0, -35.0)),
    pole_l=Vector((0.5, 0.2, -1.0)), pole_r=None,
    stand={35: 4.0, 36: 7.0, 37: 8.0, 38: 6.0, 39: 4.0, 40: 2.0}, stand_dir_in=Vector((0.3, 0.6, -1.0)), stand_dir_out=Vector((1.0, 0.3, 0.2)),
)


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def s62(f):
    return smooth((f - 62) / 11.0)


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
        q = qa.slerp(qb, t)
        out[n] = Matrix.Translation(la.lerp(lb, t)) @ q.to_matrix().to_4x4() @ Matrix.Diagonal((*sa.lerp(sb, t), 1.0))
    return out


def euler_rot(x, y, z):
    return Euler((math.radians(x), math.radians(y), math.radians(z)), 'XYZ').to_matrix().to_4x4()


def Rx(d):
    return Matrix.Rotation(math.radians(d), 4, 'X')


def Ry(d):
    return Matrix.Rotation(math.radians(d), 4, 'Y')


G34 = SRCW[34]['ik_hand_gun'].copy()
G83 = SRCW[83]['ik_hand_gun'].copy()


def gmat(loc, rot4):
    m = rot4.copy()
    m.translation = loc
    return m


def gun_keys():
    R_level = G83.to_3x3().to_4x4()
    G_level = gmat(P['level_loc'], R_level)
    R_cock = Matrix.Rotation(math.radians(P['cock_yaw']), 4, 'Z') @ Rx(-P['cock_pitch']) @ Ry(P['cock_roll']) @ R_level
    G_cock = gmat(P['cock_loc'], R_cock)
    R_pull = Rx(-P['pull_pitch']) @ R_cock
    G_pull = gmat(P['cock_loc'] + P['pull_off'], R_pull)
    return G_cock, G_pull, G_level


TENSION = {41: 0.0, 42: 0.08, 43: 0.30, 44: 0.65, 45: 1.0, 46: 1.1}
KICK = {48: 0.0, 49: 0.55, 50: 0.95, 51: 1.0, 52: 0.78, 53: 0.40, 54: 0.12, 55: -0.04, 56: 0.0}


def gun_matrix(f):
    """ik_hand_gun world matrix for the new clip, f in 34..83."""
    G_cock, G_pull, G_level = gun_keys()
    if f <= 34:
        return G34.copy()
    if f >= 73:
        return SRCW[f]['ik_hand_gun'].copy()
    if f < 41:
        return mat_blend(G34, G_cock, smooth((f - 34) / 7.0))
    if f <= 46:
        return _extrap(G_cock, G_pull, TENSION[f])
    if f <= 48:
        return mat_blend(_extrap(G_cock, G_pull, TENSION[46]), G_level, (f - 46) / 2.0)
    if f <= 56:
        k = KICK[f]
        G = G_level.copy()
        R = Rx(-P['kick_pitch'] * k) @ G_level.to_3x3().to_4x4()
        return gmat(G_level.translation + Vector((0, P['kick_back'] * k, 0)), R)
    if f <= 62:
        return G_level.copy()
    return mat_blend(G_level, SRCW[f]['ik_hand_gun'], s62(f))


def _extrap(A, B, T):
    if T <= 1.0:
        return mat_blend(A, B, T)
    # continue past B along the same translation, same extra rotation
    qa, qb = A.to_quaternion(), B.to_quaternion()
    axis, ang = (qa.inverted() @ qb).to_axis_angle()
    q = qa @ Quaternion(axis, ang * T)
    M = q.to_matrix().to_4x4()
    M.translation = A.translation + (B.translation - A.translation) * T
    return M


def slide_y(f):
    if f < 41:
        return 4.2
    if f <= 46:
        return 4.2 + 0.5 * min(1.0, TENSION[f])
    if f == 46:
        return 4.7
    return 0.0


# ---------------------------------------------------------------- left hand grip
GRIP = {}


def setup_grip():
    best = tune_tube(P['grip_radius'])
    deg = best[1]
    b, F, r, M = tube_for(float(deg))
    H = M['hand_l']
    FT = F.inverted() @ M['middle_03_l'].translation
    zs = -1.0 if FT.y > 0 else 1.0
    if P['zs_flip']:
        zs = -zs
    a = math.radians(P['grip_a'])
    x = Vector((math.sin(a), 0.0, math.cos(a)))
    if P.get('grip_x') is not None:
        x = P['grip_x'].normalized()
    z = Vector((0.0, zs, 0.0))
    if P.get('grip_z') is not None:
        z = P['grip_z'].normalized()
    x = (x - z * x.dot(z)).normalized() if abs(x.dot(z)) < 0.99 and P.get('grip_x') is None else x
    z = (z - x * z.dot(x)).normalized()
    y = z.cross(x)
    Fg = Matrix((x, y, z)).transposed().to_4x4()
    Fg.translation = P['grip_c'] + P['grip_shift']
    te = P['thumb_eul']
    thumb = {n: euler_rot(*te[3 * i:3 * i + 3]) for i, n in enumerate(THUMB)}
    b = fingers_curl_bases(SRCB[34], {f: deg * P['grip_t'][f] for f in FINGERS}, thumb)
    GRIP.update(curl=deg, radius=r, bases=b, hand_in_tube=F.inverted() @ H, Fg=Fg, zs=zs, FT=FT)
    GRIP['Lgrip'] = Fg @ GRIP['hand_in_tube']
    GRIP['fingers'] = {n: b[n] for n in LFING + THUMB}
    GRIP['open'] = {n: open_left(SRCB[34])[n] for n in LFING + THUMB}
    return GRIP


def lgrip(dy=0.0):
    L = GRIP['Lgrip'].copy()
    L.translation = L.translation + Vector((0.0, dy, 0.0))
    return L


def left_state(f, G, s):
    H, fb = _left_state(f, G, s)
    D = P['stand'].get(f, 0.0)
    if D:
        d = P['stand_dir_in'] if f < 44 else P['stand_dir_out']
        H = H.copy()
        H.translation = H.translation + G.to_3x3() @ (d.normalized() * D)
    return H, fb


def _left_state(f, G, s):
    """Returns (hand_l world matrix, left finger bases dict)."""
    HL34 = SRCW[34]['hand_l']
    fing34 = {n: SRCB[34][n] for n in LFING + THUMB}
    OPEN = GRIP['open']
    GR = GRIP['fingers']
    G41 = gun_matrix(41)
    Hgrip41 = G41 @ lgrip(0.0)
    if f < 41:
        t = 0.1 * smooth((f - 34) / 7.0) + 0.9 * ((f - 34) / 7.0)
        H = mat_blend(HL34, Hgrip41, t)
        if f <= 37:
            fb = bases_blend(fing34, OPEN, smooth((f - 34) / 3.0))
        else:
            fb = bases_blend(OPEN, GR, smooth((f - 37) / 4.0))
        return H, fb
    if f <= 46:
        return G @ lgrip(slide_y(f) - 4.2), dict(GR)
    # released at 47: fly away down-left in armature space
    H46 = gun_matrix(46) @ lgrip(0.5)
    Rfin = Matrix.Rotation(math.radians(P['hov_yaw']), 4, 'Z') @ Matrix.Rotation(math.radians(-P['hov_pitch']), 4, 'X') @ SRCW[73]['hand_l'].to_3x3().to_4x4()
    Rfin.translation = SRCW[73]['hand_l'].translation
    HOV = None

    G46 = gun_matrix(46)
    Lout = (G46.to_3x3() @ GRIP['Fg'].to_3x3().col[0]).normalized()  # palm direction = the way out of the grip

    def hover(ff):
        # drop straight down first, then left; target wrist (10, -25, -35), held low and off-screen
        T = P['hover_target']
        t = max(0.0, min(1.0, (ff - 46) / 7.0))
        ez = 0.5 * t + 0.5 * smooth(t)
        ex = ez
        pos = Vector((H46.translation.x + (T.x - H46.translation.x) * ex,
                      H46.translation.y + (T.y - H46.translation.y) * ex,
                      H46.translation.z + (T.z - H46.translation.z) * ez))
        R = mat_blend(H46, Rfin, P['fly_rot'] * smooth((min(ff, 62) - P['rot_start']) / (58.0 - P['rot_start'])))
        R.translation = pos
        return R

    open_frames = P.get('open_frames', 2.0)
    fo = bases_blend(GR, OPEN, smooth((f - 46) / open_frames)) if f < 46 + open_frames else dict(OPEN)
    if f <= 62:
        return hover(f), fo
    H62 = hover(62)
    Lrel = SRCW[f]['ik_hand_gun'].inverted() @ SRCW[f]['hand_l']
    Hsrc = G @ Lrel
    fsrc = {n: SRCB[f][n] for n in LFING + THUMB}
    return mat_blend(H62, Hsrc, s), bases_blend(OPEN, fsrc, s)


TW = {}  # previous twist angle per side, for unwrapping


def solve_arm_cont(bases, side, T, pole, ref_M):
    miss = solve_arm(bases, side, T, pole, ref_M)
    return miss


def pose_frame(f):
    if f <= 34 or f >= 73:
        TWIST_LAST['r'] = TWIST_LAST['l'] = 0.0
        return {k: v.copy() for k, v in SRCB[f].items()}, {}
    s = s62(f)
    b = {k: v.copy() for k, v in SRCB[f].items()}  # body, spine and clavicles stay exactly as in the source
    rf = [n for n in b if n.endswith('_r') and any(k in n for k in FINGERS + ('thumb',)) and n.split('_')[1].startswith('0')]
    b.update(bases_blend(SRCB[34], SRCB[f], s, rf))  # right-hand fingers keep the frame-34 grip until the blend back to the source
    ref_M = SRCM[f]
    M = fk(b)
    G = gun_matrix(f)
    set_world(b, M, 'ik_hand_gun', G)
    info = {}
    # right hand rides the grip
    Tr = G @ SRCW[f]['ik_hand_gun'].inverted() @ SRCW[f]['hand_r']
    info['miss_r'] = solve_arm(b, 'r', Tr, arm_bend(SRCM[f], 'r'), ref_M)
    # left hand
    Tl, lf = left_state(f, G, s)
    bl = arm_bend(SRCM[f], 'l')
    pole_l = bl.lerp(P['pole_l'].normalized(), smooth((f - 34) / 7.0) * (1.0 - s)).normalized()
    info['miss_l'] = solve_arm(b, 'l', Tl, pole_l, ref_M)
    for n, m in lf.items():
        b[n] = m
    M = fk(b)
    for side in 'rl':
        Hsrc = SRCW[f][f'hand_{side}']
        set_world(b, M, f'ik_hand_{side}', M[f'hand_{side}'] @ Hsrc.inverted() @ SRCW[f][f'ik_hand_{side}'])
        M = fk(b)
    info['G'] = G
    return b, info


# ---------------------------------------------------------------- bake
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


def build_arms(name='A_FP_Handgun_ReloadEmpty_Arms'):
    old = bpy.data.actions.get(name)
    if old:
        bpy.data.actions.remove(old)
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    lp.animation_data.action = act
    prev_q, infos = {}, {}
    for f in range(F0, F1 + 1):
        b, info = pose_frame(f)
        apply_bases(b)
        key_all(f, prev_q)
        infos[f] = info
    # object level curves from the source
    for fc in SRC_ARMS.fcurves:
        if fc.data_path.startswith('pose.bones'):
            continue
        n = act.fcurves.new(fc.data_path, index=fc.array_index)
        n.keyframe_points.add(len(fc.keyframe_points))
        for k, s in zip(n.keyframe_points, fc.keyframe_points):
            k.co = s.co
            k.interpolation = s.interpolation
            k.handle_left, k.handle_right = s.handle_left, s.handle_right
            k.handle_left_type, k.handle_right_type = s.handle_left_type, s.handle_right_type
        n.update()
    for fc in act.fcurves:
        for k in fc.keyframe_points:
            k.interpolation = 'LINEAR' if fc.data_path.startswith('pose.bones') else k.interpolation
    return act, infos


def build_gun(name='A_FP_WEP_Handgun_ReloadEmpty_Gun'):
    old = bpy.data.actions.get(name)
    if old:
        bpy.data.actions.remove(old)
    act = SRC_GUN.copy()
    act.name = name
    act.use_fake_user = True
    for fc in list(act.fcurves):
        if not fc.data_path.startswith('pose.bones'):
            act.fcurves.remove(fc)
    fc = next(c for c in act.fcurves if c.data_path == 'pose.bones["slide"].location' and c.array_index == 1)
    for k in fc.keyframe_points:
        f = int(round(k.co[0]))
        if f >= 35:
            k.co[1] = slide_y(f)
            k.handle_left[1] = k.handle_right[1] = slide_y(f)
        k.interpolation = 'LINEAR'
    fc.update()
    return act


def bake_all():
    setup_grip()
    gun_act = build_gun()
    gun.animation_data.action = gun_act
    arms_act, infos = build_arms()
    scene.frame_start, scene.frame_end = F0, F1
    scene.frame_set(34)
    return arms_act, gun_act, infos


def fit_standoff(frames, Dmax=16.0, step=0.5, tol=0.3):
    """Offline helper: for each frame find the smallest standoff D (cm, along the stand direction in gun space) with no skin
    penetration deeper than tol against the pistol. Returns {frame: D}."""
    out = {}
    saved = dict(P['stand'])
    for f in frames:
        scene.frame_set(f)
        bv = gun_bvh_state()
        found = None
        D = 0.0
        while D <= Dmax:
            P['stand'][f] = D
            b, info = pose_frame(f)
            apply_bases(b)
            res, n = penetration(bv, info['G'])
            if (max(res.values()) if res else -9) <= tol:
                found = D
                break
            D += step
        out[f] = found
    P['stand'] = saved
    return out
