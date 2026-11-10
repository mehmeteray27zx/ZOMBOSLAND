# Loader: execute the modules in order in the shared namespace, then bake.
import bpy, os
_D = r"E:\ZOMBOSLAND\SourceArt\HandgunReload"
for _n in ('handgun_reload_rig.py', 'handgun_reload_grip.py', 'handgun_reload_keys.py', 'handgun_reload_overhand.py', 'handgun_reload_check.py'):
    _p = os.path.join(_D, _n)
    exec(compile(open(_p, encoding='utf8').read(), _p, 'exec'), globals())
