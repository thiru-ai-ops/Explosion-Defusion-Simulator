#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Writes Logs/BDS_BombParts.txt: every renderer under the suitcase bomb with its materials/colour and bounds. Diagnostic only.</summary>
[InitializeOnLoad]
public static class BDSBombPartsDump
{
    static BDSBombPartsDump()
    {
        string marker = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "BDS_BombParts.txt");
        if (File.Exists(marker)) return;
        EditorApplication.delayCall += Dump;
    }

    [MenuItem("Tools/BDS/Debug/Dump Bomb Parts")]
    public static void Dump()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var sb = new StringBuilder();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                string path = r.name;
                Transform t = r.transform.parent;
                bool underBomb = false;
                while (t != null) { path = t.name + "/" + path; if (t.name == "Suitcasebomb" || t.name.StartsWith("cut0")) underBomb = true; t = t.parent; }
                if (!underBomb && !r.name.StartsWith("cut0")) continue;
                sb.Append(path).Append(" | active=").Append(r.gameObject.activeInHierarchy);
                Mesh m = r.GetComponent<MeshFilter>() ? r.GetComponent<MeshFilter>().sharedMesh : null;
                sb.Append(" | mesh=").Append(m ? m.name + " v" + m.vertexCount + " sub" + m.subMeshCount : "-");
                Bounds b = r.bounds;
                sb.Append($" | center={b.center.x:F3},{b.center.y:F3},{b.center.z:F3} size={b.size.x:F3},{b.size.y:F3},{b.size.z:F3}");
                foreach (Material mat in r.sharedMaterials)
                {
                    if (mat == null) { sb.Append(" | mat=null"); continue; }
                    Color c = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : (mat.HasProperty("_Color") ? mat.color : Color.white);
                    Texture tex = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : (mat.HasProperty("_MainTex") ? mat.mainTexture : null);
                    sb.Append($" | mat={mat.name} shader={mat.shader.name} col={ColorUtility.ToHtmlStringRGB(c)} tex={(tex ? tex.name : "-")}");
                }
                sb.AppendLine();
            }
        }
        string file = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "BDS_BombParts.txt");
        File.WriteAllText(file, sb.ToString());
        Debug.Log("[BDS] Bomb parts written to " + file);
    }
}
#endif
