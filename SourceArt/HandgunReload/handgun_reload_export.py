# Exports the two handgun reload clips with the same FBX settings as SourceArt\Axe\axe_export.py, then saves a copy of the working file.
# Run after handgun_reload_keys.py has baked (bake_all) in the same namespace.
import os, shutil

OUT = r"E:\ZOMBOSLAND\Assets\Art\Viewmodels\HandgunReload"
SRC_DIR = r"E:\ZOMBOSLAND\SourceArt\HandgunReload"
os.makedirs(OUT, exist_ok=True)
os.makedirs(SRC_DIR, exist_ok=True)


def _export(obj, temp_name, original_name, filename):
    if bpy.context.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    obj.name = temp_name
    try:
        bpy.ops.export_scene.fbx(
            filepath=os.path.join(OUT, filename), use_selection=True, object_types={'ARMATURE', 'MESH'},
            apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
            add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
            use_armature_deform_only=False, bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
            bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
            mesh_smooth_type='FACE', use_tspace=True, path_mode='STRIP', embed_textures=False)
    finally:
        obj.name = original_name


def export_all(arms_act, gun_act):
    scene.frame_start, scene.frame_end = F0, F1
    scene.frame_set(1)
    lp.animation_data.action = arms_act
    gun.animation_data.action = gun_act
    _export(lp, 'Armature', 'LPSP_Rig', 'A_FP_Handgun_ReloadEmpty_Arms.fbx')
    # The pistol follows ik_hand_gun in the game, so the GunToIK constraint must not be baked into the gun file: mute it while exporting.
    con = gun.constraints['GunToIK']
    was_muted = con.mute
    con.mute = True
    bpy.context.view_layer.update()
    try:
        _export(gun, 'SKEL_Handgun_03', 'Gun_Rig', 'A_FP_WEP_Handgun_ReloadEmpty_Gun.fbx')
    finally:
        con.mute = was_muted
        bpy.context.view_layer.update()
    scene.frame_set(1)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SRC_DIR, 'handgun_reload.blend'), copy=True, compress=True)
    print('exported', sorted(os.listdir(OUT)), [os.path.getsize(os.path.join(OUT, n)) for n in sorted(os.listdir(OUT))])
