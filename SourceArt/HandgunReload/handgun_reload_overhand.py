# Overhand slide rack: pistol looks forward with its top rolled toward the left hand, the left palm closes on top of the rear slide.
# Run after handgun_reload_keys.py in the same namespace. Replaces setup_grip and the cock pose parameters.
# Gun space: cm, muzzle -Y, top +Z, gun right -X. The rear of the slide is +Y.

P.update(
    cock_yaw=-15.0, cock_loc=Vector((-7.0, -29.0, -9.0)), cock_pitch=35.0, cock_roll=35.0, zs_flip=False,
    hover_target=Vector((18.0, -25.0, -36.0)), open_frames=5.0,
    pull_off=Vector((-0.3, -0.9, 0.0)), pull_pitch=3.0,
    stand={35: 3.0, 36: 7.0, 37: 10.0, 38: 11.0, 39: 8.0, 40: 3.5}, stand_dir_in=Vector((1.0, 0.1, 0.45)), stand_dir_out=Vector((1.0, 0.1, 0.45)),
    pole_l=Vector((0.9, 0.1, -0.7)),
)

OV = dict(
    palm=Vector((3.3, 10.0, 8.45)),     # palm centre resting on top of the locked-back slide
    finger=Vector((-0.88, -0.22, -0.14)),  # wrist to knuckles: across the top toward the gun's right, slanted slightly to the muzzle
    back=Vector((0.18, 0.0, 1.0)),      # back of the hand: up off the slide
    curl=dict(index=(8.0, 68.0, 34.0), middle=(10.0, 68.0, 34.0), ring=(-6.0, 74.0, 37.0), pinky=(38.0, 26.0, 13.0)),
    thumb=[0.0, 12.0, 54.0, 12.0, 0.0, 0.0, 12.0, 0.0, 0.0],
)


def _hand_frame(M):
    h, i, m, p = (M[f'{n}_l'].translation for n in ('hand', 'index_01', 'middle_01', 'pinky_01'))
    f = (m - h).normalized()
    n = (i - h).cross(p - h).normalized()   # back of the hand (source support grip: points away from the pistol)
    n = (n - f * n.dot(f)).normalized()
    s = n.cross(f)
    return Matrix((f, s, n)).transposed(), (h + i + m + p) / 4.0


def _curl_sign(base):
    b = {k: v.copy() for k, v in base.items()}
    curl_fingers(b, 'l', {f: (40.0, 40.0, 40.0) for f in FINGERS})
    M = fk(b)
    C, _ = _hand_frame(fk(base))
    return 1.0 if (M['middle_03_l'].translation - M['middle_01_l'].translation).dot(C.col[2]) < 0.0 else -1.0


def setup_grip():
    base = open_left(SRCB[34])
    M0 = fk(base)
    C, pc = _hand_frame(M0)
    H3 = M0['hand_l'].to_3x3().normalized()
    f_t = OV['finger'].normalized()
    n_t = (OV['back'] - f_t * OV['back'].dot(f_t)).normalized()
    s_t = n_t.cross(f_t)
    Ct = Matrix((f_t, s_t, n_t)).transposed()
    Rt = Ct @ C.inverted() @ H3
    off = H3.inverted() @ (pc - M0['hand_l'].translation)
    L = Rt.to_4x4()
    L.translation = OV['palm'] - Rt @ off
    sg = _curl_sign(base)
    b = {k: v.copy() for k, v in base.items()}
    curl_fingers(b, 'l', {f: tuple(sg * c for c in OV['curl'][f]) for f in FINGERS})
    te = OV['thumb']
    for i, n in enumerate(THUMB):
        b[n] = euler_rot(*te[3 * i:3 * i + 3])
    F = Matrix.Identity(4)
    F.col[0][:3] = Vector((0.0, 0.0, 1.0))
    GRIP.clear()
    GRIP.update(curl=0.0, radius=0.0, bases=b, hand_in_tube=L, Fg=Matrix.Identity(4), zs=1.0, FT=Vector((0, 0, 0)), Lgrip=L, curl_sign=sg)
    GRIP['fingers'] = {n: b[n] for n in LFING + THUMB}
    GRIP['open'] = {n: open_left(SRCB[34])[n] for n in LFING + THUMB}
    return GRIP


def grip_report(frames=(41, 44, 46)):
    out = []
    for fr in frames:
        scene.frame_set(fr)
        bpy.context.view_layer.update()
        pb = lp.pose.bones
        P_ = lambda n: pb[n].matrix.translation.copy()
        h, i, m, p = P_('hand_l'), P_('index_01_l'), P_('middle_01_l'), P_('pinky_01_l')
        n = (i - h).cross(p - h).normalized()
        palm = (h + i + m + p) / 4.0
        G = pb['ik_hand_gun'].matrix
        Ginv = G.inverted()
        top = G @ (gun.pose.bones['slide'].matrix @ Vector((0.0, 8.0, 0.0)))
        fw = (P_('lowerarm_l') - h).normalized()
        fh = (m - h).normalized()
        out.append(dict(f=fr, n_gun=[round(c, 2) for c in (Ginv.to_3x3() @ n)], palm_gun=[round(c, 1) for c in Ginv @ palm],
                        finger_gun=[round(c, 2) for c in (Ginv.to_3x3() @ fh)], thumb_tip_gun=[round(c, 1) for c in Ginv @ P_('thumb_03_l')],
                        middle_tip_gun=[round(c, 1) for c in Ginv @ P_('middle_03_l')],
                        wrist_bend=round(180.0 - math.degrees(fw.angle(fh)), 1), hand_l=[round(c, 1) for c in h], elbow=[round(c, 1) for c in P_('lowerarm_l')]))
    return out
