# Keys for the axe clips. Run after axe_rig.py in the same namespace.
# (frame, grip position, haft direction, blade hint, body offset, ease of the segment ending at this key)
IDLE = (Vector((-17.0, -36.0, -7.0)), Vector((-0.5, -0.3, 0.8)), Vector((0.55, -0.8, -0.2)), Vector((0.0, 0.0, 0.0)))
KEYS = [
    (0, *IDLE, 'smooth'),
    (12, Vector((-24.0, -30.0, -2.0)), Vector((-0.30, 0.15, 0.94)), Vector((0.20, -0.90, 0.30)), Vector((-9.0, -4.0, 0.0)), 'smooth'),
    (15, Vector((-25.0, -28.0, -1.0)), Vector((-0.30, 0.22, 0.93)), Vector((0.20, -0.90, 0.35)), Vector((-10.0, -4.5, 0.0)), 'smooth'),
    (18, Vector((-14.0, -40.0, -10.0)), Vector((-0.12, -0.62, 0.77)), Vector((0.05, -0.75, -0.65)), Vector((0.0, -4.0, -4.0)), 'in'),
    (21, Vector((-12.0, -40.0, -26.0)), Vector((0.55, -0.80, 0.10)), Vector((0.0, 0.0, -1.0)), Vector((-3.0, -11.0, -12.0)), 'linear'),
    (24, Vector((-11.0, -41.0, -28.0)), Vector((0.58, -0.78, 0.05)), Vector((0.0, -0.05, -1.0)), Vector((-3.0, -11.5, -13.0)), 'out'),
    (36, Vector((-15.0, -40.0, -20.0)), Vector((0.10, -0.75, 0.65)), Vector((0.30, -0.60, -0.70)), Vector((0.0, -5.0, -6.0)), 'smooth'),
    (60, *IDLE, 'smooth'),
]
TOTAL = 60


def ease(t, kind):
    if kind == 'in':
        return t * t
    if kind == 'out':
        return 1.0 - (1.0 - t) * (1.0 - t)
    if kind == 'linear':
        return t
    return t * t * (3.0 - 2.0 * t)


def key_matrix(k):
    return axe_matrix(k[1], k[2], k[3])


def sample(frame):
    for a, b in zip(KEYS, KEYS[1:]):
        if a[0] <= frame <= b[0]:
            t = ease((frame - a[0]) / float(b[0] - a[0]), b[5])
            Ma, Mb = key_matrix(a), key_matrix(b)
            qa, qb = Ma.to_quaternion(), Mb.to_quaternion()
            q = qa.slerp(qb, t)
            grip = a[1].lerp(b[1], t)
            A = q.to_matrix().to_4x4()
            A.translation = grip - A.to_3x3() @ GRIP['r'][0]
            return A, a[4].lerp(b[4], t)
    raise ValueError(frame)


def key_all(frame):
    for pb in lp.pose.bones:
        pb.keyframe_insert('location', frame=frame, group=pb.name)
        pb.keyframe_insert('rotation_quaternion', frame=frame, group=pb.name)
        pb.keyframe_insert('scale', frame=frame, group=pb.name)


def bake(name, frames, get):
    old = bpy.data.actions.get(name)
    if old:
        bpy.data.actions.remove(old)
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    lp.animation_data.action = act
    worst = 0.0
    for out_frame, f in frames:
        A, body = get(f)
        b, miss = pose_for(A, body=body)
        worst = max(worst, miss['r'], miss['l'])
        apply_bases(b)
        key_all(out_frame)
    return act, worst


# Output frames start at 1, as in the LPSP clips.
idle_act, idle_miss = bake('A_FP_Axe_Idle', [(1, 0), (2, 0)], sample)
swing_act, swing_miss = bake('A_FP_Axe_Swing', [(f + 1, f) for f in range(TOTAL + 1)], sample)
scene = bpy.context.scene
scene.frame_start, scene.frame_end = 1, TOTAL + 1

# Axe follows hand_r rigidly: parent it to the bone in the idle pose, so a single child transform serves every clip.
lp.animation_data.action = None
A0, body0 = sample(0)
b0, _ = pose_for(A0, body=body0)
apply_bases(b0)
axe.parent = lp
axe.parent_type = 'BONE'
axe.parent_bone = 'hand_r'
axe.matrix_parent_inverse = Matrix.Identity(4)
bpy.context.view_layer.update()
axe.matrix_world = A0 @ Matrix.Diagonal((100.0, 100.0, 100.0, 1.0))
bpy.context.view_layer.update()
lp.animation_data.action = swing_act
print('miss idle', round(idle_miss, 2), 'swing', round(swing_miss, 2), 'fcurves', len(swing_act.fcurves))
