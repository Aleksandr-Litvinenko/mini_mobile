using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Moba.EditorTools
{
    public static class ModelProbe
    {
        public static void Run()
        {
            foreach (var name in new[]
                     {
                         "Wizard", "Witch", "Elf", "GreenSpikyBlob", "Mushnub",
                         "WatchTower_SecondAge_Level3", "Crystal2", "Tree1", "Rock1"
                     })
            {
                string path = "Assets/Resources/Models/" + name + ".fbx";
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null)
                {
                    Debug.Log("[Probe] " + name + ": NOT FOUND");
                    continue;
                }
                var rends = go.GetComponentsInChildren<Renderer>(true);
                Bounds b = new Bounds(go.transform.position, Vector3.zero);
                bool first = true;
                foreach (var r in rends)
                {
                    if (first) { b = r.bounds; first = false; }
                    else b.Encapsulate(r.bounds);
                }
                var clips = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview"))
                    .Select(c => c.name).ToArray();
                var mats = rends.SelectMany(r => r.sharedMaterials)
                    .Where(m => m != null).Distinct()
                    .Select(m => m.name + (m.mainTexture != null ? "(tex)" : "(noTex)"))
                    .ToArray();
                Debug.Log("[Probe] " + name + " size=" + b.size + " skinned=" +
                          (go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) +
                          "\n  clips: " + string.Join(", ", clips) +
                          "\n  mats: " + string.Join(", ", mats));
            }
        }
    }
}
