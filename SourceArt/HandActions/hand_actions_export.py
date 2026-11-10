# Exports the hand action clips (armature only), the bat mesh and textures, the working .blend and preview videos.
# Run after hand_actions_rig.py and hand_actions_keys.py + build() in the same namespace. FBX settings match SourceArt/Axe/axe_export.py.
import os
import numpy as np

OUT = r"E:\ZOMBOSLAND\Assets\Art\Viewmodels\HandActions"
BAT_OUT = r"E:\ZOMBOSLAND\Assets\Art\Viewmodels\Bat"
SRC_DIR = r"E:\ZOMBOSLAND\SourceArt\HandActions"
PREVIEW = os.path.join(SRC_DIR, "preview")
for _d in (OUT, BAT_OUT, PREVIEW):
    os.makedirs(_d, exist_ok=True)


def _export(path, with_bat):
    if bpy.context.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT')
    lp.select_set(True)
    if with_bat:
        bat.hide_viewport = False
        bat.select_set(True)
    bpy.context.view_layer.objects.active = lp
    lp.name = 'Armature'
    try:
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
            apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
            add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
            use_armature_deform_only=False, bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
            bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
            mesh_smooth_type='FACE', use_tspace=True, path_mode='STRIP', embed_textures=False)
    finally:
        lp.name = 'LPSP_Rig'


def export_clips(names=None):
    done = []
    for name, (f0, f1) in sorted(CLIPS.items()):
        if names and name not in names:
            continue
        lp.animation_data.action = bpy.data.actions[name]
        scene.frame_start, scene.frame_end = f0, f1
        scene.frame_set(f0)
        bpy.context.view_layer.update()
        _export(os.path.join(BAT_OUT if name == 'A_FP_Bat_Idle' else OUT, name + '.fbx'), name == 'A_FP_Bat_Idle')
        done.append(name)
    return done


def _pixels(img):
    a = np.empty(img.size[0] * img.size[1] * 4, dtype=np.float32)
    img.pixels.foreach_get(a)
    return a


def _save(name, arr, w, h, colorspace):
    im = bpy.data.images.new(name, w, h, alpha=True, float_buffer=False)
    im.colorspace_settings.name = colorspace
    im.alpha_mode = 'STRAIGHT'
    im.pixels.foreach_set(arr)
    im.filepath_raw = os.path.join(BAT_OUT, name + '.png')
    im.file_format = 'PNG'
    im.save()
    bpy.data.images.remove(im)


def export_bat_textures():
    images = {n.image.name: n.image for n in bat.data.materials[0].node_tree.nodes if n.type == 'TEX_IMAGE' and n.image}
    rough = images['baseball_bat_rough.png']
    normal = images['baseball_bat_nor_gl.png']
    diff = images['baseball_bat_diff.png']
    w, h = rough.size
    r = _pixels(rough)
    mask = np.ones_like(r)  # project convention: R metallic, G AO, A smoothness
    mask[0::4] = 0.0
    mask[3::4] = np.clip(1.0 - r[0::4], 0, 1)
    _save('T_Bat_mask', mask, w, h, 'Non-Color')
    n = np.clip(_pixels(normal), 0, 1)
    n[3::4] = 1.0
    _save('T_Bat_normal', n, w, h, 'Non-Color')
    _save('T_Bat_albedo', _pixels(diff), w, h, 'sRGB')
    return sorted(os.listdir(BAT_OUT))


def setup_preview_render():
    r = scene.render
    r.engine = 'BLENDER_WORKBENCH'
    r.resolution_x, r.resolution_y, r.resolution_percentage = 960, 540, 100
    r.fps = 30
    r.image_settings.file_format = 'FFMPEG'
    r.ffmpeg.format = 'MPEG4'
    r.ffmpeg.codec = 'H264'
    r.ffmpeg.constant_rate_factor = 'HIGH'
    d = scene.display.shading
    d.light = 'STUDIO'
    d.color_type = 'TEXTURE'
    d.show_shadows = False
    d.background_type = 'WORLD' if hasattr(d, 'background_type') else d.background_type


def render_preview(name):
    preview(name)
    scene.render.filepath = os.path.join(PREVIEW, name + '.mp4')
    bpy.ops.render.render(animation=True)
    return os.path.getsize(scene.render.filepath)
