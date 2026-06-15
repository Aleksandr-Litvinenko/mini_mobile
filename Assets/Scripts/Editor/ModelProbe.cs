using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Moba.EditorTools
{
    public static class ModelProbe
    {
        public static void Run()
        {
            foreach (var rel in new[]
                     {
                         "Models/TD/tower-round-build-a.fbx",
                         "Models/TD/tower-round-build-d.fbx",
                         "Models/TD/tower-round-build-f.fbx",
                         "Models/TD/tower-round-crystals.fbx",
                         "Models/TD/detail-crystal-large.fbx",
                         "Models/tree_detailed.fbx",
                         "Models/tree_pineDefaultA.fbx",
                         "Models/tree_fat.fbx",
                         "Models/plant_bushLarge.fbx"
                     })
            {
                string path = "Assets/Resources/" + rel;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) { Debug.Log("[Probe] MISSING " + rel); continue; }
                var rends = go.GetComponentsInChildren<Renderer>(true);
                Bounds b = default; bool first = true;
                foreach (var r in rends)
                {
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                }
                var mats = rends.SelectMany(r => r.sharedMaterials).Where(m => m != null)
                    .Select(m => m.name + (m.mainTexture != null ? "+tex(" + m.mainTexture.name + ")" : "+NOTEX"))
                    .Distinct().ToArray();
                Debug.Log("[Probe] " + rel.Substring(rel.LastIndexOf('/') + 1) +
                          " size=" + b.size + " mats=[" + string.Join(",", mats) + "]");
            }
        }
    }
}
