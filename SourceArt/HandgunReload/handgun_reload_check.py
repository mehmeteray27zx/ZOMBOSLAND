# Measurements on the baked actions. Run after handgun_reload_keys.py has baked (bake_all).
def sample_action(act, frames=range(F0, F1 + 1)):
    lp.animation_data.action = act
    out = {}
    for f in frames:
        scene.frame_set(f)
        out[f] = {pb.name: (pb.matrix_basis.copy(), pb.matrix.copy()) for pb in lp.pose.bones}
    return out


def rot_angle(A, B):
    return math.degrees(2.0 * math.acos(min(1.0, abs(A.to_quaternion().dot(B.to_quaternion())))))


def report(act, gun_act):
    S = sample_action(act)
    r = {}
    for n in ('hand_r', 'hand_l'):
        worst, worst_out, wf = 0.0, 0.0, None
        for f in range(F0 + 1, F1 + 1):
            a_loc = rot_angle(S[f - 1][n][0], S[f][n][0])
            a_wld = rot_angle(S[f - 1][n][1], S[f][n][1])
            a = max(a_loc, a_wld)
            worst = max(worst, a)
            if 34 < f < 73 and not (46 < f <= 48):
                if a > worst_out:
                    worst_out, wf = a, f
        r[n] = (round(worst, 1), round(worst_out, 1), wf)
    # bone scale range
    smin, smax = 9, -9
    for f in S:
        for n, (mb, mw) in S[f].items():
            s = mb.to_scale()
            smin, smax = min(smin, min(s)), max(smax, max(s))
    # pose difference vs source at frames 34 and 83
    diff = {}
    for f in (34, 73, 83):
        d = 0.0
        for n in S[f]:
            d = max(d, (S[f][n][1].translation - SRCW[f][n].translation).length)
        diff[f] = round(d, 4)
    # bone length stretch (head-to-child distances) vs frame 1
    stretch = 0.0
    for f in S:
        for n in ('upperarm_r', 'lowerarm_r', 'upperarm_l', 'lowerarm_l'):
            a = S[f][n][1].translation
            child = {'upperarm_r': 'lowerarm_r', 'lowerarm_r': 'hand_r', 'upperarm_l': 'lowerarm_l', 'lowerarm_l': 'hand_l'}[n]
            b = S[f][child][1].translation
            ref = (SRCW[1][n].translation - SRCW[1][child].translation).length
            stretch = max(stretch, abs((a - b).length - ref))
    # clavicle/shoulder deviation from source frame f
    dev = max((S[f][n][1].translation - SRCW[f][n].translation).length for f in S for n in ('clavicle_l', 'clavicle_r', 'upperarm_l', 'upperarm_r'))
    devsrc = {f: round(max((S[f][n][1].translation - SRCW[f][n].translation).length for n in ('clavicle_l', 'clavicle_r', 'upperarm_l', 'upperarm_r')), 1) for f in range(34, 84, 4)}
    # slide
    gun.animation_data.action = gun_act
    sl = {}
    for f in (44, 45, 46, 47, 48):
        scene.frame_set(f)
        sl[f] = round(gun.pose.bones['slide'].location[1], 3)
    return dict(rot=r, scale=(round(smin, 5), round(smax, 5)), diff=diff, stretch_cm=round(stretch, 4), shoulder_dev_cm=round(dev, 3), dev_vs_src=devsrc, slide=sl)


from mathutils.bvhtree import BVHTree as _BVH
_RIGHT_NAMES = [vg.index for vg in arm_obj.vertex_groups if vg.name.endswith('_r') and any(k in vg.name for k in ('hand', 'index', 'middle', 'ring', 'pinky', 'thumb', 'lowerarm'))]
_RVERTS = {v.index for v in arm_obj.data.vertices if any(g.group in _RIGHT_NAMES and g.weight > 0.5 for g in v.groups)}


def hand_vs_hand(f):
    """Left-hand skin vertices inside the right hand / right forearm skin at frame f (count, deepest cm, group)."""
    scene.frame_set(f)
    ev = arm_obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    me = ev.to_mesh()
    me.calc_loop_triangles()
    co = [v.co.copy() for v in me.vertices]
    tris = [tuple(t.vertices) for t in me.loop_triangles if all(i in _RVERTS for i in t.vertices)]
    bv = _BVH.FromPolygons(co, tris)
    worst, wg, cnt = -9.0, None, 0
    for vi, gn in LEFT_IDS.items():
        loc, nor, idx, dist = bv.find_nearest(co[vi], 3.0)
        if loc is not None and (co[vi] - loc).dot(nor) < 0:
            cnt += 1
            if dist > worst:
                worst, wg = dist, gn
    ev.to_mesh_clear()
    return cnt, round(worst, 2), wg
