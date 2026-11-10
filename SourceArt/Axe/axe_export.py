# Exports the axe clips, the textures and the working .blend. Run after axe_rig.py and axe_swing.py in the same namespace.
import os, shutil
import numpy as np

OUT = r"E:\ZOMBOSLAND\Assets\Art\Viewmodels\Axe"
SRC_DIR = r"E:\ZOMBOSLAND\SourceArt\Axe"
os.makedirs(OUT, exist_ok=True)
os.makedirs(SRC_DIR, exist_ok=True)


def export_clip(name, action, last, with_axe):
    scene = bpy.context.scene
    lp.animation_data.action = action
    scene.frame_start, scene.frame_end = 1, last
    scene.frame_set(1)
    bpy.ops.object.mode_set(mode='OBJECT') if bpy.context.mode != 'OBJECT' else None
    bpy.ops.object.select_all(action='DESELECT')
    lp.select_set(True)
    if with_axe:
        axe.select_set(True)
    bpy.context.view_layer.objects.active = lp
    lp.name = 'Armature'
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT, name + '.fbx'), use_selection=True, object_types={'ARMATURE', 'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
        use_armature_deform_only=False, bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
        mesh_smooth_type='FACE', use_tspace=True, path_mode='STRIP', embed_textures=False)
    lp.name = 'LPSP_Rig'


export_clip('A_FP_Axe_Idle', idle_act, 2, True)
export_clip('A_FP_Axe_Swing', swing_act, TOTAL + 1, False)


def pixels(img):
    a = np.empty(img.size[0] * img.size[1] * 4, dtype=np.float32)
    img.pixels.foreach_get(a)
    return a


def save_png(name, arr, w, h):
    im = bpy.data.images.new(name, w, h, alpha=True, float_buffer=False)
    im.colorspace_settings.name = 'Non-Color'
    im.alpha_mode = 'STRAIGHT'
    im.pixels.foreach_set(arr)
    im.filepath_raw = os.path.join(OUT, name + '.png')
    im.file_format = 'PNG'
    im.save()
    bpy.data.images.remove(im)


images = {n.image.name: n.image for n in axe.data.materials[0].node_tree.nodes if n.type == 'TEX_IMAGE' and n.image}
rough, metal = images['wooden_axe_02_rough.png'], images['wooden_axe_02_metal.png']
normal, diff = images['wooden_axe_02_nor_gl.png'], images['wooden_axe_02_diff.png']
w, h = rough.size
# Project convention (_mask): R = metallic, G = AO (none in the source: 1), A = smoothness.
r = pixels(rough)
m = pixels(metal)
mask = np.ones_like(r)
mask[0::4] = np.clip(m[0::4], 0, 1)
mask[3::4] = np.clip(1.0 - r[0::4], 0, 1)
save_png('T_Axe_mask', mask, w, h)
n = np.clip(pixels(normal), 0, 1)
n[3::4] = 1.0
save_png('T_Axe_normal', n, w, h)
alb = bpy.data.images.new('T_Axe_albedo', w, h, alpha=True, float_buffer=False)
alb.colorspace_settings.name = 'sRGB'
alb.alpha_mode = 'STRAIGHT'
alb.pixels.foreach_set(pixels(diff))
alb.filepath_raw = os.path.join(OUT, 'T_Axe_albedo.png')
alb.file_format = 'PNG'
alb.save()
bpy.data.images.remove(alb)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SRC_DIR, 'axe_viewmodel.blend'), copy=True, compress=True)
for f in ('axe_rig.py', 'axe_swing.py', 'axe_export.py'):
    shutil.copyfile(os.path.join(SCRIPTS, f), os.path.join(SRC_DIR, f))

bpy.context.scene.frame_set(1)
bpy.context.view_layer.update()
checks = {}
for label, act, f in (('idle', idle_act, 1), ('impact', swing_act, 22)):
    lp.animation_data.action = act
    bpy.context.scene.frame_set(f)
    bpy.context.view_layer.update()
    checks[label] = {n: [round(c, 3) for c in lp.pose.bones[n].head] for n in ('hand_r', 'hand_l', 'lowerarm_r')}
    checks[label]['axe_head'] = [round(c, 3) for c in axe.matrix_world @ Vector((0.0, -0.05, 0.40))]
print('CHECK', checks)
print('files', sorted(os.listdir(OUT)))
