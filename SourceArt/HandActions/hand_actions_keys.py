# Hand action clips, baked per frame at 30 fps. Run after hand_actions_rig.py in the same namespace.
# Carriers: gun (pistol), hands (empty), bat, axe. Commit frames must match Docs/SPEC_v0.8_hands_loop.md.
# Rules from the references: keep the wrist slightly bent but under WRIST_LIMIT from the forearm, spread forearm roll over the twist bone,
# reach at the crosshair, close the fingers on the commit frame.
import numpy as np

WRIST_LIMIT = 36.0
G0 = IDLE_M['ik_hand_gun'].copy()
HR0 = IDLE_M['hand_r'].copy()
HL0 = IDLE_M['hand_l'].copy()


def fingers_base(names):
    return {k: (Matrix.Identity(4) if k in names else v.copy()) for k, v in IDLE.items()}


OPEN = {'l': curl_bases(fingers_base(LF), 'l', {f: (-26.0, -12.0, -6.0) for f in FINGERS}),
        'r': curl_bases(fingers_base(RF), 'r', {f: (-26.0, -12.0, -6.0) for f in FINGERS})}
GRAB = {'l': curl_bases(fingers_base(LF), 'l', {'index': (50, 62, 36), 'middle': (55, 66, 38), 'ring': (58, 66, 38), 'pinky': (60, 62, 36)}),
        'r': curl_bases(fingers_base(RF), 'r', {'index': (50, 62, 36), 'middle': (55, 66, 38), 'ring': (58, 66, 38), 'pinky': (60, 62, 36)})}
RELAX = {'l': curl_bases(fingers_base(LF), 'l', {f: (16.0, 26.0, 16.0) for f in FINGERS}),
         'r': curl_bases(fingers_base(RF), 'r', {f: (16.0, 26.0, 16.0) for f in FINGERS})}
FNAMES = {'l': LF, 'r': RF}


def _hand_axes(M, side):
    h, mid, ix, pk = (M[n].translation for n in (f'hand_{side}', f'middle_01_{side}', f'index_01_{side}', f'pinky_01_{side}'))
    F = (mid - h).normalized()
    A = ix - pk
    A = (A - F * A.dot(F)).normalized()
    return F, A, F.cross(A).normalized()


def _palm_sign(side):
    F, A, N = _hand_axes(IDLE_M, side)
    curl = IDLE_M[f'middle_03_{side}'].translation - IDLE_M[f'middle_01_{side}'].translation
    return 1.0 if curl.dot(N) > 0 else -1.0


PALM = {s: _palm_sign(s) for s in 'lr'}
IDLE_AXES = {s: Matrix(_hand_axes(IDLE_M, s)).transposed() for s in 'lr'}


def hand_rot(side, fingers, palm):
    F = fingers.normalized()
    P = (palm - F * palm.dot(F)).normalized()
    N = P * PALM[side]
    A = N.cross(F).normalized()
    R = Matrix((F, A, N)).transposed() @ IDLE_AXES[side].inverted()
    return (R @ rot_of(IDLE_M[f'hand_{side}'])).normalized().to_4x4()


def catmull(keys, f):
    fs = sorted(keys)
    if f <= fs[0]:
        return keys[fs[0]].copy()
    if f >= fs[-1]:
        return keys[fs[-1]].copy()
    i = max(k for k in range(len(fs)) if fs[k] <= f)
    f1, f2 = fs[i], fs[i + 1]
    P1, P2 = keys[f1], keys[f2]
    f0 = fs[i - 1] if i > 0 else f1 - (f2 - f1)
    f3 = fs[i + 2] if i + 2 < len(fs) else f2 + (f2 - f1)
    P0 = keys[fs[i - 1]] if i > 0 else P1 - (P2 - P1)
    P3 = keys[fs[i + 2]] if i + 2 < len(fs) else P2 + (P2 - P1)
    h = f2 - f1
    m1 = (P2 - P0) / (f2 - f0) * h
    m2 = (P3 - P1) / (f3 - f1) * h
    t = (f - f1) / h
    t2, t3 = t * t, t * t * t
    return P1 * (2 * t3 - 3 * t2 + 1) + m1 * (t3 - 2 * t2 + t) + P2 * (-2 * t3 + 3 * t2) + m2 * (t3 - t2)


def slerp_keys(keys, f):
    fs = sorted(keys)
    if f <= fs[0]:
        return keys[fs[0]].copy()
    if f >= fs[-1]:
        return keys[fs[-1]].copy()
    i = max(k for k in range(len(fs)) if fs[k] <= f)
    a, b = fs[i], fs[i + 1]
    return mat_blend(keys[a], keys[b], smooth((f - a) / (b - a)))


def fingers_at(keys, f, names):
    fs = sorted(keys)
    if f <= fs[0]:
        return {n: keys[fs[0]][n] for n in names}
    if f >= fs[-1]:
        return {n: keys[fs[-1]][n] for n in names}
    i = max(k for k in range(len(fs)) if fs[k] <= f)
    a, b = fs[i], fs[i + 1]
    return bases_blend(keys[a], keys[b], smooth((f - a) / (b - a)), names)


def gun_at(loc_off, pitch, yaw=0.0, roll=0.0):
    R = Rz(yaw) @ Rx(pitch) @ Ry(roll) @ G0.to_3x3().to_4x4()
    R.translation = G0.translation + loc_off
    return R


def lead(b, side, v):
    M = fk(b)
    n = f'clavicle_{side}'
    T = M[n].copy()
    T.translation = T.translation + v
    set_world(b, M, n, T)


def finish_ik_helpers(b):
    M = fk(b)
    for side in 'rl':
        set_world(b, M, f'ik_hand_{side}', M[f'hand_{side}'] @ IDLE_M[f'hand_{side}'].inverted() @ IDLE_M[f'ik_hand_{side}'])
        M = fk(b)


def set_gun(b, G):
    M = fk(b)
    set_world(b, M, 'ik_hand_gun', G)


def hide_gun(b, amount):
    if amount <= 0.0:
        return
    loc, q, s = b['ik_hand_gun'].decompose()
    k = max(0.002, 1.0 - amount)
    b['ik_hand_gun'] = Matrix.Translation(loc) @ q.to_matrix().to_4x4() @ Matrix.Diagonal((s.x * k, s.y * k, s.z * k, 1.0))


WRIST_LOG = {}


def wrist_bend(M, side):
    forearm = (M[f'hand_{side}'].translation - M[f'lowerarm_{side}'].translation).normalized()
    fingers = (M[f'middle_01_{side}'].translation - M[f'hand_{side}'].translation).normalized()
    return math.degrees(forearm.angle(fingers))


IDLE_BEND = {s: wrist_bend(IDLE_M, s) for s in 'lr'}


def solve_limited(b, side, loc, R, pole, ref_M, limit=WRIST_LIMIT):
    """IK with a wrist limit: the hand may bend at most `limit` degrees from the forearm (or the bend it already has in the reference pose)."""
    T = R.copy()
    T.translation = loc
    miss = solve_arm(b, side, T, pole, ref_M)
    M = fk(b)
    allowed = max(limit, wrist_bend(ref_M, side) + 2.0)
    forearm = (M[f'hand_{side}'].translation - M[f'lowerarm_{side}'].translation).normalized()
    fingers = (M[f'middle_01_{side}'].translation - M[f'hand_{side}'].translation).normalized()
    ang = math.degrees(forearm.angle(fingers))
    if ang > allowed:
        axis = fingers.cross(forearm).normalized()
        Fix = Matrix.Rotation(math.radians(ang - allowed), 4, axis)
        T = (Fix @ rot_of(T).to_4x4()).normalized()
        T.translation = loc
        miss = solve_arm(b, side, T, pole, ref_M)
    return miss


def note(clip, b):
    M = fk(b)
    log = WRIST_LOG.setdefault(clip, {'l': 0.0, 'r': 0.0})
    for s in 'lr':
        log[s] = max(log[s], wrist_bend(M, s))


# ---------------------------------------------------------------- carriers
def bare_base():
    b = {k: v.copy() for k, v in IDLE.items()}
    TWIST_LAST.clear()
    Rl = hand_rot('l', Vector((-0.12, -0.93, 0.34)), Vector((-0.5, 0.2, -0.85)))
    Rr = hand_rot('r', Vector((0.12, -0.93, 0.34)), Vector((0.5, 0.2, -0.85)))
    solve_limited(b, 'r', Vector((-17.0, -28.0, -25.0)), Rr, Vector((-0.5, 0.1, -1.0)), IDLE_M)
    solve_limited(b, 'l', Vector((16.0, -28.0, -25.0)), Rl, Vector((0.6, 0.1, -1.0)), IDLE_M)
    b.update({n: RELAX['l'][n] for n in LF})
    b.update({n: RELAX['r'][n] for n in RF})
    finish_ik_helpers(b)
    return b


# Bat: local +Z runs from the knob to the tip, origin 25 cm above the knob. Two-hand tape grip, right hand on top.
bat = bpy.data.objects['baseball_bat']
if not bat.data.get('bat_axis_z'):
    bat.data.transform(Matrix.Rotation(math.radians(90.0), 4, 'X'))
    bat.rotation_euler = (0.0, 0.0, 0.0)
    bat.data['bat_axis_z'] = True
    bat.data.update()


def circle_center(points, z_axis):
    p0 = points[0]
    u = z_axis.orthogonal().normalized()
    v = z_axis.cross(u).normalized()
    xy = np.array([[(p - p0).dot(u), (p - p0).dot(v)] for p in points])
    a = np.c_[2 * xy, np.ones(len(xy))]
    c = (xy ** 2).sum(axis=1)
    s = np.linalg.lstsq(a, c, rcond=None)[0]
    r = math.sqrt(max(s[2] + s[0] ** 2 + s[1] ** 2, 0.0))
    return p0 + u * float(s[0]) + v * float(s[1]), r


def hand_tube(M, side):
    z = (M[f'index_01_{side}'].translation - M[f'pinky_01_{side}'].translation).normalized()
    pts = [M[f'middle_0{i}_{side}'].translation.copy() for i in (1, 2, 3)]
    pts.append(M[f'middle_03_{side}'].translation + (M[f'middle_03_{side}'].translation - M[f'middle_02_{side}'].translation) * 0.8)
    c, r = circle_center(pts, z)
    palm = (M[f'hand_{side}'].translation + M[f'middle_01_{side}'].translation) * 0.5
    x = palm - c
    x = (x - z * x.dot(z)).normalized()
    y = z.cross(x)
    F = Matrix((x, y, z)).transposed().to_4x4()
    F.translation = c
    return F, r


def fit_grip(side, radius):
    best = None
    names = FNAMES[side]
    for deg in range(24, 76, 2):
        b = curl_bases(fingers_base(names), side, {f: (deg, deg * 1.15, deg * 0.7) for f in FINGERS})
        F, r = hand_tube(fk(b), side)
        if best is None or abs(r - radius) < best[0]:
            best = (abs(r - radius), deg, b, F)
    return best[2], best[3], best[1]


GRIP_B = {}
HAND_IN_TUBE = {}
for _s in 'rl':
    _b, _F, _deg = fit_grip(_s, 3.0)
    GRIP_B[_s] = _b
    HAND_IN_TUBE[_s] = _F.inverted() @ fk(_b)[f'hand_{_s}']

STATION = {'r': (Vector((0.0, 0.0, -9.0)), 20.0), 'l': (Vector((0.0, 0.0, -20.0)), -18.0)}
BPOLE = {'r': Vector((-0.75, 0.15, -1.0)), 'l': Vector((0.8, 0.15, -1.0))}


def on_haft(side):
    c, ang = STATION[side]
    a = math.radians(ang)
    x = Vector((math.sin(a), math.cos(a), 0.0))
    z = Vector((0.0, 0.0, 1.0))
    y = z.cross(x)
    F = Matrix((x, y, z)).transposed().to_4x4()
    F.translation = c
    return F


def bat_matrix(grip, direction, hint):
    z = direction.normalized()
    h = (hint - z * hint.dot(z)).normalized()
    y = -h
    x = y.cross(z)
    A = Matrix((x, y, z)).transposed().to_4x4()
    A.translation = grip - A.to_3x3() @ STATION['r'][0]
    return A


BAT_IDLE = (Vector((-18.0, -36.0, -17.0)), Vector((-0.42, -0.10, 0.90)), Vector((0.55, -0.8, -0.2)))


def bat_pose(A, left_on=1.0, body=None, base_l=None):
    b = {k: v.copy() for k, v in IDLE.items()}
    if body is not None and body.length > 1e-4:
        M = fk(b)
        set_world(b, M, 'root', Matrix.Translation(body) @ M['root'])
    Tr = A @ on_haft('r') @ HAND_IN_TUBE['r']
    solve_limited(b, 'r', Tr.translation, rot_of(Tr).to_4x4(), BPOLE['r'], IDLE_M, 170.0)  # the grip wins; STATION angles keep the wrist natural
    Tl = A @ on_haft('l') @ HAND_IN_TUBE['l']
    if left_on >= 1.0 or base_l is None:
        solve_limited(b, 'l', Tl.translation, rot_of(Tl).to_4x4(), BPOLE['l'], IDLE_M, 170.0)
    else:
        M_l = mat_blend(base_l, Tl, left_on)
        solve_limited(b, 'l', M_l.translation, rot_of(M_l).to_4x4(), BPOLE['l'], IDLE_M, 170.0)
    b.update({n: GRIP_B['r'][n] for n in RF})
    b.update(bases_blend(RELAX['l'], GRIP_B['l'], left_on, LF))
    finish_ik_helpers(b)
    return b


def place_bat(A):
    bat.parent = None
    bat.matrix_world = A @ Matrix.Diagonal((100.0, 100.0, 100.0, 1.0))
    bpy.context.view_layer.update()


def tune_stations():
    # Roll each hand around the handle until the wrist is as straight as the grip allows.
    for s in 'rl':
        best = None
        c = STATION[s][0]
        for ang in range(-90, 91, 5):
            STATION[s] = (c, float(ang))
            TWIST_LAST.clear()
            b = bat_pose(bat_matrix(*BAT_IDLE))
            bend = wrist_bend(fk(b), s)
            if best is None or bend < best[0]:
                best = (bend, float(ang))
        STATION[s] = (c, best[1])
    return {s: STATION[s][1] for s in 'rl'}


STATION_FIT = tune_stations()
TWIST_LAST.clear()
CARR = {'gun': IDLE, 'hands': bare_base(), 'bat': bat_pose(bat_matrix(*BAT_IDLE))}
_axe = bpy.data.actions.get('A_FP_Axe_Swing')  # frame 1 is the axe idle pose, baked on this same rig
if _axe:
    CARR['axe'] = source_bases(_axe, 1)
CARR_M = {k: fk(v) for k, v in CARR.items()}


# ---------------------------------------------------------------- lower amount for the holding arm
LOWER = {
    'gun': dict(loc=Vector((-3.0, 1.0, -6.0)), pitch=13.0, roll=-5.0),
    'hands': dict(loc=Vector((-1.0, 0.0, -3.0)), pitch=4.0, roll=0.0),
    'bat': dict(loc=Vector((-3.0, 7.0, -7.0)), pitch=12.0, roll=-3.0),
    'axe': dict(loc=Vector((-3.0, 7.0, -7.0)), pitch=12.0, roll=-3.0),
}
DEEP = {
    'gun': dict(loc=Vector((-5.0, 6.0, -21.0)), pitch=30.0, roll=-12.0),
    'hands': dict(loc=Vector((-2.0, 2.0, -9.0)), pitch=6.0, roll=0.0),
    'bat': dict(loc=Vector((-8.0, 6.0, -22.0)), pitch=24.0, roll=-8.0),
    'axe': dict(loc=Vector((-8.0, 6.0, -22.0)), pitch=24.0, roll=-8.0),
}


def reach_side(c):
    return 'r' if c == 'hands' else 'l'


def hold_arm(b, c, side, a, params, ref):
    """Moves the holding arm by a fraction a of `params`. side is the arm that keeps the weapon."""
    M0 = CARR_M[c]
    H = M0[f'hand_{side}']
    if c == 'gun':
        G = gun_at(params['loc'] * a, params['pitch'] * a, 0.0, params['roll'] * a)
        set_gun(b, G)
        T = G @ G0.inverted() @ H
    else:
        R = Rx(params['pitch'] * a) @ Ry(params['roll'] * a) @ H.to_3x3().to_4x4()
        T = R.copy()
        T.translation = H.translation + params['loc'] * a
    return solve_limited(b, side, T.translation, rot_of(T).to_4x4(), arm_bend(M0, side), M0, 48.0)


# ---------------------------------------------------------------- pickup: 13 frames (0.40 s), fingers close on frame 7 (0.20 s)
REACH = {
    'l': dict(lead=Vector((-10.0, -30.0, -12.0)), pre=Vector((8.0, -40.0, -18.0)), hit=Vector((5.0, -54.0, -14.0)), back=Vector((9.0, -40.0, -22.0)),
              F5=Vector((-0.1, -0.98, 0.16)), P5=Vector((0.06, 0.15, -0.98)), F7=Vector((-0.08, -0.97, -0.22)), P7=Vector((0.06, -0.2, -0.97)),
              F9=Vector((-0.12, -0.96, -0.2)), P9=Vector((0.3, -0.2, -0.93)), off=Vector((6.0, -3.0, -6.0)), pole=Vector((0.35, 0.1, -1.0))),
    'r': dict(lead=Vector((2.0, -28.0, -12.0)), pre=Vector((-9.0, -40.0, -18.0)), hit=Vector((-5.0, -54.0, -14.0)), back=Vector((-10.0, -40.0, -22.0)),
              F5=Vector((0.1, -0.98, 0.16)), P5=Vector((-0.06, 0.15, -0.98)), F7=Vector((0.08, -0.97, -0.22)), P7=Vector((-0.06, -0.2, -0.97)),
              F9=Vector((0.12, -0.96, -0.2)), P9=Vector((-0.3, -0.2, -0.93)), off=Vector((-6.0, -3.0, -6.0)), pole=Vector((-0.45, 0.1, -1.0))),
}


def pose_pickup(c, f, clip):
    b = {k: v.copy() for k, v in CARR[c].items()}
    M0 = CARR_M[c]
    s = reach_side(c)
    other = 'l' if s == 'r' else 'r'
    a = smooth((f - 1) / 4.0) if f < 9 else 1.0 - smooth((f - 9) / 4.0)
    reach = smooth((f - 1) / 5.0) if f < 8 else 1.0 - smooth((f - 8) / 5.0)
    k = REACH[s]
    if c == 'hands':
        miss_o = hold_arm(b, c, other, a, LOWER[c], M0)
    else:
        miss_o = hold_arm(b, c, 'r', a, LOWER[c], M0)
    lead(b, s, k['lead'] * reach)
    H0 = M0[f'hand_{s}']
    loc = {1: H0.translation.copy(), 3: H0.translation + k['off'], 5: k['pre'], 7: k['hit'], 9: k['back'], 11: H0.translation + k['off'] + Vector((0, 0, -2.0)), 13: H0.translation.copy()}
    rot = {1: H0.to_3x3().to_4x4(), 3: mat_blend(H0.to_3x3().to_4x4(), hand_rot(s, k['F5'], k['P5']), 0.5),
           5: hand_rot(s, k['F5'], k['P5']), 7: hand_rot(s, k['F7'], k['P7']), 9: hand_rot(s, k['F9'], k['P9']), 13: H0.to_3x3().to_4x4()}
    pole = arm_bend(M0, s).lerp(k['pole'].normalized(), reach).normalized()
    miss = solve_limited(b, s, catmull(loc, f), slerp_keys(rot, f), pole, M0)
    base_f = {n: CARR[c][n] for n in FNAMES[s]}
    b.update(fingers_at({1: base_f, 3: OPEN[s], 6: OPEN[s], 7: GRAB[s], 9: GRAB[s], 12: base_f}, f, FNAMES[s]))
    finish_ik_helpers(b)
    note(clip, b)
    return b, ({'miss': round(miss, 2), 'o': round(miss_o, 2)} if max(miss, miss_o) > 0.05 else None)


# ---------------------------------------------------------------- lower / raise (pistol only: UI, doors)
def pose_lower(f, last, down):
    b = {k: v.copy() for k, v in IDLE.items()}
    a = smooth((f - 1) / (last - 1.0))
    if not down:
        a = 1.0 - a
    hold_arm(b, 'gun', 'r', a, DEEP['gun'], IDLE_M)
    Ll = HL0.copy()
    Tl = G0.inverted() @ Ll
    G = gun_at(DEEP['gun']['loc'] * a, DEEP['gun']['pitch'] * a, 0.0, DEEP['gun']['roll'] * a)
    Tw = G @ Tl
    solve_limited(b, 'l', Tw.translation, rot_of(Tw).to_4x4(), arm_bend(IDLE_M, 'l'), IDLE_M, 48.0)
    finish_ik_helpers(b)
    return b, None


# ---------------------------------------------------------------- open container: 25 frames, lid grabbed on 9, lifted by 15 (commit 0.47 s)
OPENK = {
    'l': dict(lead=Vector((-8.0, -24.0, -10.0)), grab=Vector((6.0, -48.0, -22.0)), top=Vector((6.0, -46.0, -2.0)), F=Vector((-0.08, -0.95, -0.3)), P=Vector((0.05, -0.3, -0.95)), pole=Vector((0.4, 0.1, -1.0))),
    'r': dict(lead=Vector((2.0, -24.0, -10.0)), grab=Vector((-6.0, -48.0, -22.0)), top=Vector((-6.0, -46.0, -2.0)), F=Vector((0.08, -0.95, -0.3)), P=Vector((-0.05, -0.3, -0.95)), pole=Vector((-0.45, 0.1, -1.0))),
}


def pose_container(c, f, clip):
    b = {k: v.copy() for k, v in CARR[c].items()}
    M0 = CARR_M[c]
    s = reach_side(c)
    k = OPENK[s]
    a = smooth((f - 1) / 6.0) if f < 19 else 1.0 - smooth((f - 19) / 6.0)
    if c == 'hands':
        hold_arm(b, c, 'l' if s == 'r' else 'r', a, DEEP[c], M0)
    else:
        hold_arm(b, c, 'r', a, DEEP[c], M0)
    reach = smooth((f - 1) / 8.0) if f < 16 else 1.0 - smooth((f - 16) / 9.0)
    lead(b, s, k['lead'] * reach)
    H0 = M0[f'hand_{s}']
    loc = {1: H0.translation.copy(), 5: (H0.translation + k['grab']) * 0.5 + Vector((0, 0, -3)), 9: k['grab'], 11: k['grab'] + Vector((0, 1.0, 4.0)), 15: k['top'], 17: k['top'] + Vector((0, 0, 1.0)), 21: (H0.translation + k['top']) * 0.5 + Vector((0, 0, -8)), 25: H0.translation.copy()}
    Rg = hand_rot(s, k['F'], k['P'])
    Rt = hand_rot(s, k['F'] + Vector((0, 0, 0.45)), k['P'] + Vector((0, 0.5, 0.2)))
    rot = {1: H0.to_3x3().to_4x4(), 9: Rg, 15: Rt, 25: H0.to_3x3().to_4x4()}
    pole = arm_bend(M0, s).lerp(k['pole'].normalized(), reach).normalized()
    miss = solve_limited(b, s, catmull(loc, f), slerp_keys(rot, f), pole, M0, 42.0)
    base_f = {n: CARR[c][n] for n in FNAMES[s]}
    b.update(fingers_at({1: base_f, 5: OPEN[s], 8: OPEN[s], 9: GRAB[s], 16: GRAB[s], 21: OPEN[s], 25: base_f}, f, FNAMES[s]))
    finish_ik_helpers(b)
    note(clip, b)
    return b, ({'miss': round(miss, 2)} if miss > 0.05 else None)


# ---------------------------------------------------------------- bat
SWING = [
    (1, *BAT_IDLE, Vector((0, 0, 0))),
    (5, Vector((-25.0, -29.0, -3.0)), Vector((-0.30, 0.16, 0.94)), Vector((0.20, -0.90, 0.30)), Vector((-9.0, -4.0, 0.0))),
    (6, Vector((-25.0, -28.0, -2.0)), Vector((-0.30, 0.22, 0.93)), Vector((0.20, -0.90, 0.35)), Vector((-10.0, -4.5, 0.0))),
    (8, Vector((-13.0, -42.0, -12.0)), Vector((0.12, -0.70, 0.70)), Vector((0.05, -0.75, -0.65)), Vector((-1.0, -5.0, -5.0))),
    (10, Vector((-11.0, -43.0, -26.0)), Vector((0.62, -0.76, 0.12)), Vector((0.0, 0.0, -1.0)), Vector((-3.0, -11.0, -12.0))),
    (12, Vector((-10.0, -42.0, -29.0)), Vector((0.70, -0.70, -0.08)), Vector((0.0, -0.05, -1.0)), Vector((-3.0, -11.5, -13.0))),
    (16, Vector((-15.0, -40.0, -22.0)), Vector((0.10, -0.75, 0.65)), Vector((0.30, -0.60, -0.70)), Vector((0.0, -5.0, -6.0))),
    (21, *BAT_IDLE, Vector((0, 0, 0))),
]


def _swing_at(f, keys):
    for a, b in zip(keys, keys[1:]):
        if a[0] <= f <= b[0]:
            t = (f - a[0]) / float(b[0] - a[0])
            t = smooth(t)
            Ma, Mb = bat_matrix(a[1], a[2], a[3]), bat_matrix(b[1], b[2], b[3])
            A = mat_blend(Ma, Mb, t).to_3x3().to_4x4()
            grip = a[1].lerp(b[1], t)
            A.translation = grip - A.to_3x3() @ STATION['r'][0]
            return A, a[4].lerp(b[4], t)
    return bat_matrix(*BAT_IDLE), Vector((0, 0, 0))


def pose_bat_swing(f):
    A, body = _swing_at(f, SWING)
    b = bat_pose(A, body=body)
    note('A_FP_Bat_Swing', b)
    return b, None


BAT_DRAW = [
    (1, Vector((-27.0, -24.0, -40.0)), Vector((-0.82, 0.30, 0.48)), Vector((0.55, -0.7, -0.4)), Vector((0, 0, 0))),
    (5, Vector((-25.0, -34.0, -24.0)), Vector((-0.68, -0.05, 0.73)), Vector((0.55, -0.8, -0.2)), Vector((0, 0, 0))),
    (9, Vector((-20.0, -36.0, -13.0)), Vector((-0.50, -0.20, 0.84)), Vector((0.55, -0.8, -0.2)), Vector((0, 0, 0))),
    (12, Vector((-17.5, -37.0, -14.5)), Vector((-0.38, -0.16, 0.91)), Vector((0.55, -0.8, -0.2)), Vector((0, 0, 0))),
    (15, Vector((-18.5, -35.5, -18.0)), Vector((-0.44, -0.08, 0.89)), Vector((0.55, -0.8, -0.2)), Vector((0, 0, 0))),
    (18, *BAT_IDLE, Vector((0, 0, 0))),
]
LEFT_LOW = Matrix.Translation(Vector((20.0, -18.0, -44.0))) @ HL0.to_3x3().to_4x4()


def pose_bat_draw(f):
    A, body = _swing_at(f, BAT_DRAW)
    on = smooth((f - 8) / 5.0)
    b = bat_pose(A, left_on=on, base_l=LEFT_LOW, body=body)
    note('A_FP_Bat_Draw', b)
    return b, None


def pose_axe_draw(f):
    """Generic draw: the axe idle pose comes up from below with a small overshoot."""
    b = {k: v.copy() for k, v in CARR['axe'].items()}
    M0 = CARR_M['axe']
    t = (f - 1) / 17.0
    drop = 1.0 - smooth(min(1.0, t / 0.62))
    over = math.sin(max(0.0, (t - 0.62) / 0.38) * math.pi) * 0.1
    for s in 'rl':
        H = M0[f'hand_{s}']
        R = Rx(26.0 * drop - 7.0 * over) @ Rz(-10.0 * drop) @ H.to_3x3().to_4x4()
        loc = H.translation + Vector((-6.0, 12.0, -30.0)) * drop + Vector((0.0, 0.0, 2.2)) * over
        solve_limited(b, s, loc, R, arm_bend(M0, s), M0, 48.0)
    finish_ik_helpers(b)
    return b, None


# ---------------------------------------------------------------- bare hands idle (loop)
def pose_hands_idle(f):
    b = {k: v.copy() for k, v in CARR['hands'].items()}
    M0 = CARR_M['hands']
    p = (f - 1) / 60.0 * 2.0 * math.pi
    for s, ph in (('r', 0.0), ('l', 0.9)):
        H = M0[f'hand_{s}']
        loc = H.translation + Vector((0.25 * math.sin(p + ph), 0.2 * math.cos(p * 2.0 + ph), 0.45 * math.sin(p + ph)))
        solve_limited(b, s, loc, H.to_3x3().to_4x4(), arm_bend(M0, s), M0)
    finish_ik_helpers(b)
    return b, None


# ---------------------------------------------------------------- bandage: 52 frames, healing on frame 37 (1.20 s)
def bandage_left(f):
    H0 = HL0
    up = smooth((f - 3) / 9.0) if f < 41 else 1.0 - smooth((f - 41) / 11.0)
    W = Vector((5.0, -31.0, -19.0))
    loc = H0.translation.lerp(W, up) + Vector((0.0, 0.0, -2.5)) * math.sin(min(1.0, (f - 3) / 9.0) * math.pi) if f < 13 else H0.translation.lerp(W, up)
    R = mat_blend(H0.to_3x3().to_4x4(), hand_rot('l', Vector((-0.72, -0.62, 0.30)), Vector((0.10, -0.32, 0.94))), up)
    return loc, R, up


def wrap_center(b):
    M = fk(b)
    W, E = M['hand_l'].translation, M['lowerarm_l'].translation
    axis = (W - E).normalized()
    return W - axis * 8.0, axis


def pose_bandage(f):
    b = {k: v.copy() for k, v in IDLE.items()}
    gdown = smooth((f - 1) / 6.0) if f < 44 else 1.0 - smooth((f - 44) / 8.0)
    G = gun_at(Vector((-6.0, 8.0, -34.0)) * gdown, 42.0 * gdown, 0.0, -12.0 * gdown)
    set_gun(b, G)
    TR = G @ G0.inverted() @ HR0
    lead_l = smooth((f - 3) / 9.0) if f < 41 else 1.0 - smooth((f - 41) / 11.0)
    lead(b, 'l', Vector((-8.0, -20.0, -10.0)) * lead_l)
    loc_l, R_l, up = bandage_left(f)
    pole_l = arm_bend(IDLE_M, 'l').lerp(Vector((0.55, 0.15, -1.0)).normalized(), up).normalized()
    solve_limited(b, 'l', loc_l, R_l, pole_l, IDLE_M, 44.0)
    C, axis = wrap_center(b)
    u = axis.orthogonal().normalized()
    if u.z < 0.0:
        u = -u
    v = axis.cross(u).normalized()
    orbit = max(0.0, min(1.0, (f - 14) / 20.0))
    theta = smooth(orbit) * 4.0 * math.pi + math.pi * 0.2
    radial = (u * math.cos(theta) + v * math.sin(theta)).normalized()
    drift = axis * (1.5 - 3.5 * orbit)
    orbit_loc = C + radial * 9.5 + drift
    # enter and leave the orbit from the low right; the tie is a short pull up and out
    hand_in = smooth((f - 8) / 6.0) if f < 40 else 1.0 - smooth((f - 40) / 8.0)
    tie = smooth((f - 34) / 3.0) * (1.0 - smooth((f - 38) / 3.0))
    low = Vector((-22.0, -22.0, -34.0))
    loc_r = low.lerp(orbit_loc + (u * 3.0 + axis * 2.0) * tie, hand_in)
    F_r = (-radial + axis * 0.18).normalized()
    P_r = (axis + radial * 0.15).normalized()
    R_r = hand_rot('r', F_r, P_r)
    if f <= 14:
        R_r = mat_blend(HR0.to_3x3().to_4x4(), R_r, smooth((f - 2) / 8.0))
    elif f >= 40:
        R_r = mat_blend(R_r, HR0.to_3x3().to_4x4(), smooth((f - 40) / 10.0))
    if f < 44 and f > 7:
        lead(b, 'r', Vector((2.0, -20.0, -10.0)) * hand_in)
        solve_limited(b, 'r', loc_r, R_r, Vector((-0.55, 0.15, -1.0)), IDLE_M, 62.0)
        b.update(fingers_at({8: IDLE, 12: GRAB['r'], 39: GRAB['r'], 44: IDLE}, f, RF))
    else:
        solve_limited(b, 'r', TR.translation, rot_of(TR).to_4x4(), arm_bend(IDLE_M, 'r'), IDLE_M, 48.0)
    b.update(fingers_at({3: IDLE, 11: RELAX['l'], 44: RELAX['l'], 51: IDLE}, f, LF))
    hide_gun(b, 1.0 if 8 <= f <= 44 else 0.0)
    finish_ik_helpers(b)
    note('A_FP_Handgun_Bandage', b)
    return b, None


# ---------------------------------------------------------------- craft: looping work motion, 31 frames (frame 31 = frame 1)
def pose_craft(f):
    b = {k: v.copy() for k, v in IDLE.items()}
    p = (f - 1) / 30.0 * 2.0 * math.pi
    G = gun_at(Vector((-6.0, 8.0, -34.0)), 42.0, 0.0, -12.0)
    set_gun(b, G)
    lead(b, 'l', Vector((-6.0, -14.0, -8.0)))
    lead(b, 'r', Vector((2.0, -14.0, -8.0)))
    Wl = Vector((9.0, -32.0, -23.0)) + Vector((0.0, 0.5 * math.sin(p + 1.0), 0.8 * math.sin(p)))
    Wr = Vector((-9.0, -33.0, -22.0)) + Vector((2.2 * math.cos(p), 1.2 * math.sin(p * 2.0), 1.6 * math.sin(p)))
    Rl = hand_rot('l', Vector((-0.45, -0.85, 0.26)), Vector((-0.55, 0.12, -0.82)))
    Rr = Rz(10.0 * math.sin(p)) @ hand_rot('r', Vector((0.42, -0.86, 0.28)), Vector((0.55, 0.12, -0.82)))
    solve_limited(b, 'l', Wl, Rl, Vector((0.55, 0.1, -1.0)), IDLE_M)
    solve_limited(b, 'r', Wr, Rr, Vector((-0.55, 0.1, -1.0)), IDLE_M)
    b.update({n: RELAX['l'][n] for n in LF})
    b.update(bases_blend(GRAB['r'], RELAX['r'], 0.5 + 0.5 * math.sin(p), RF))
    hide_gun(b, 1.0)
    finish_ik_helpers(b)
    note('A_FP_Handgun_Craft', b)
    return b, None


# ---------------------------------------------------------------- builders
CLIPS = {}


def _make(name, f0, f1, fn):
    WRIST_LOG.pop(name, None)
    act, m = bake_clip(name, f0, f1, fn)
    CLIPS[name] = (f0, f1)
    return name, m


def build(names=None):
    plan = []
    for c, title in (('gun', 'Handgun'), ('hands', 'Hands'), ('bat', 'Bat'), ('axe', 'Axe')):
        if c not in CARR:
            continue
        n = f'A_FP_{title}_Pickup'
        plan.append((n, 1, 13, (lambda f, c=c, n=n: pose_pickup(c, f, n))))
        n2 = f'A_FP_{title}_OpenContainer'
        plan.append((n2, 1, 25, (lambda f, c=c, n=n2: pose_container(c, f, n))))
    plan += [
        ('A_FP_Handgun_Lower', 1, 7, lambda f: pose_lower(f, 7, True)),
        ('A_FP_Handgun_Raise', 1, 9, lambda f: pose_lower(f, 9, False)),
        ('A_FP_Hands_Idle', 1, 61, pose_hands_idle),
        ('A_FP_Bat_Idle', 1, 2, lambda f: (bat_pose(bat_matrix(*BAT_IDLE)), None)),
        ('A_FP_Bat_Swing', 1, 21, pose_bat_swing),
        ('A_FP_Bat_Draw', 1, 18, pose_bat_draw),
    ]
    plan.append(('A_FP_Handgun_Bandage', 1, 52, pose_bandage))
    plan.append(('A_FP_Handgun_Craft', 1, 31, pose_craft))
    if 'axe' in CARR:
        plan.append(('A_FP_Axe_Draw', 1, 18, pose_axe_draw))
    out = {}
    for n, f0, f1, fn in plan:
        if names and n not in names:
            continue
        nm, m = _make(n, f0, f1, fn)
        out[nm] = m
    return out


def preview(name):
    a = bpy.data.actions[name]
    lp.animation_data.action = a
    scene.frame_start, scene.frame_end = int(a.frame_range[0]), int(a.frame_range[1])
    scene.frame_set(int(a.frame_range[0]))
    hg = 'Handgun' not in name
    bpy.data.objects['SM_Handgun_03'].hide_viewport = bpy.data.objects['SM_Handgun_03'].hide_render = hg
    hb = 'Bat' not in name
    bat.hide_viewport = bat.hide_render = hb
    bpy.context.view_layer.update()
