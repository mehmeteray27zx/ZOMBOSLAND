# Offline search for the left-hand release / hover path (frames 47-65): no penetration of the pistol or the right hand, hand
# stays low-left on screen and out of the pistol's screen box. Prints the parameters that were pasted into P in handgun_reload_keys.py.
# Run after handgun_reload_keys.py (namespace G = bpy.app.driver_namespace). Usage: exec with G['FIT_SECONDS'] set.
import time, random
from bpy_extras.object_utils import world_to_camera_view as w2c

_P = P
_sc = scene
_cam = bpy.data.objects['Camera']
random.seed(61)
_frames = list(range(47, 66))
_BV = {}
for _f in _frames:
    _sc.frame_set(_f)
    _BV[_f] = gun_bvh_state()
_ids = list(LEFT_IDS.keys())[::5]
_gbb = [Vector(c) for c in gun_mesh.bound_box]


def _screen(Gm):
    ev = arm_obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    me = ev.to_mesh()
    pts = [w2c(_sc, _cam, arm_obj.matrix_world @ me.vertices[i].co) for i in _ids]
    ev.to_mesh_clear()
    gp = [w2c(_sc, _cam, lp.matrix_world @ (Gm @ c)) for c in _gbb]
    gx0, gx1 = min(p.x for p in gp) - 0.03, max(p.x for p in gp) + 0.03
    gy0, gy1 = min(p.y for p in gp) - 0.03, max(p.y for p in gp) + 0.03
    inbox = sum(1 for p in pts if gx0 <= p.x <= gx1 and gy0 <= p.y <= gy1) / len(pts)
    return inbox, sum(p.x for p in pts) / len(pts), (gx0 + gx1) / 2


def _disp():
    a = left_state(46, gun_matrix(46), 0)[0].translation
    b = left_state(52, gun_matrix(52), 0)[0].translation
    return b - a


def hover_cost():
    c, worst, wr, sb = 0.0, 0.0, 0.0, 0.0
    for f in _frames:
        _sc.frame_set(f)
        b, info = pose_frame(f)
        apply_bases(b)
        rb = right_hand_bvh(info['G'])
        res, n = penetration(_BV[f], info['G'], rbv=rb)
        d = max([v for k, v in res.items() if k not in ('hand_l', 'RIGHT')] or [-9])
        r = res.get('RIGHT', -9)
        worst, wr = max(worst, d), max(wr, r)
        pb = lp.pose.bones
        bend = math.degrees((pb['hand_l'].head - pb['lowerarm_l'].head).angle(pb['middle_01_l'].head - pb['hand_l'].head))
        c += 0.01 * max(0.0, bend - 60.0) ** 2
        if 54 <= f <= 62:
            c += 0.05 * max(0.0, pb['hand_l'].head.y + 21.0) ** 2  # keep the held hand away from the camera
        c += max(0, d - 0.2) ** 2 + max(0, r - 0.3) ** 2
        if 52 <= f <= 63:
            inbox, mx, gcx = _screen(info['G'])
            c += 8 * inbox ** 2 + 6 * max(0, mx - (gcx - 0.28)) ** 2
            sb = max(sb, inbox)
    dd = _disp()
    c += 0.5 * max(0, 10.5 - dd.length) ** 2 + 0.5 * max(0, dd.length - 16) ** 2 + 0.3 * max(0, 3.5 - dd.x) ** 2 + 0.3 * max(0, 5.0 - (-dd.z)) ** 2
    return c, worst, wr, sb


def _setp(x):
    _P['out_dist'], _P['fly_dist'], fx, fy, fz, _P['rot_start'], _P['hov_yaw'], _P['hov_pitch'] = x
    _P['fly_dir'] = Vector((fx, fy, fz))


def fit_hover(seconds=100):
    lo = [2, 5, 0.0, -1.0, -1.5, 47, -40, -20]
    hi = [10, 16, 1.0, 0.3, -0.2, 54, 60, 70]
    best, t0 = None, time.time()
    while time.time() - t0 < seconds * 0.6:
        x = [random.uniform(a, b) for a, b in zip(lo, hi)]
        _setp(x)
        r = hover_cost()
        if best is None or r[0] < best[0]:
            best = (r[0], x, r)
    cur, t0 = best, time.time()
    while time.time() - t0 < seconds * 0.4:
        x = [min(max(v + random.gauss(0, s), a), b) for v, s, a, b in zip(cur[1], [0.6, 0.6, 0.08, 0.05, 0.08, 0.4, 6, 6], lo, hi)]
        _setp(x)
        r = hover_cost()
        if r[0] < cur[0]:
            cur = (r[0], x, r)
    _setp(cur[1])
    print('hover fit', round(cur[0], 3), [round(v, 2) for v in cur[1]], cur[2], [round(v, 1) for v in _disp()])
    return cur
