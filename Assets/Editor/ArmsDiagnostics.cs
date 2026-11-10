using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Diagnostic only (v0.5a): dumps hierarchies / scales of the LPSP and Survivalist arms models. Menu: Zombosland/Diagnostics/Dump Arms Hierarchies.
public static class ArmsDiagnostics
{
    const string Lpsp = "Assets/ThirdParty/InfimaLPSP/Art/Meshes/Character/SK_FP_CH_Default_Cubic.fbx";
    const string Surv = "Assets/ThirdParty/Survivalist/Meshes/SK_Military_Survivalist.fbx";

    [MenuItem("Zombosland/Diagnostics/Dump Arms Hierarchies")]
    public static void Dump()
    {
        var sb = new StringBuilder();
        foreach (var path in new[] { Lpsp, Surv })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var inst = (GameObject)Object.Instantiate(prefab);
            sb.AppendLine("==== " + path + " rootScale=" + inst.transform.lossyScale.ToString("F4"));
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            sb.AppendLine("animType=" + imp.animationType + " globalScale=" + imp.globalScale + " useFileScale=" + imp.useFileScale + " fileScale=" + imp.fileScale);
            var av = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            sb.AppendLine("avatar=" + (av != null ? av.name + " human=" + av.isHuman + " valid=" + av.isValid : "none"));
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                sb.AppendLine("SMR " + smr.name + " bones=" + smr.bones.Length + " root=" + (smr.rootBone ? smr.rootBone.name : "null") + " mats=" + string.Join(",", smr.sharedMaterials.Select(m => m ? m.name : "null")) + " bounds=" + smr.localBounds.size.ToString("F3"));
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
            {
                string p = t.name; var q = t.parent;
                while (q != null && q != inst.transform.parent) { p = q.name + "/" + p; q = q.parent; }
                sb.AppendLine(p + " | lpos=" + t.localPosition.ToString("F4") + " lrot=" + t.localEulerAngles.ToString("F1") + " lscale=" + t.localScale.ToString("F3") + " world=" + t.position.ToString("F4") + " wrot=" + t.eulerAngles.ToString("F1"));
            }
            Object.DestroyImmediate(inst);
        }
        System.IO.File.WriteAllText("Logs/arms-hierarchy.txt", sb.ToString());
        Debug.Log("ARMS_DIAG_DONE");
    }
}
