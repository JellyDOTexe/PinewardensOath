using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PinewardensOath
{
    // Swaps a cloned vanilla weapon's model for one loaded from disk (OBJ + PNGs).
    // The new model is lined up with the vanilla model automatically: same haft direction (head at the end farther
    // from the hand), blade width along the vanilla model's second-longest axis, grip origin on the hand.
    // On any failure the prefab is left untouched (vanilla look) and a warning is logged.
    internal static class Visuals
    {
        private static readonly Color Glow = new Color(1f, 0.95f, 0.75f);   // only the scorch lines would glow; the Meadows emission maps are black for now

        internal static bool Apply(GameObject prefab, string dir, WeaponDef w)
        {
            try
            {
                Transform attach = prefab.transform.Find("attach") ?? throw new Exception("no 'attach' child");
                var vanilla = attach.GetComponentsInChildren<MeshFilter>(true)
                    .Where(f => f.sharedMesh != null && f.GetComponent<MeshRenderer>() != null && !Under(f.transform, "UpgraderGlow") && !Under(f.transform, "equiped"))
                    .OrderByDescending(f => f.sharedMesh.bounds.size.sqrMagnitude * f.transform.lossyScale.sqrMagnitude)
                    .FirstOrDefault() ?? throw new Exception("no vanilla mesh under attach");
                var vanillaRenderer = vanilla.GetComponent<MeshRenderer>();

                Matrix4x4 m = attach.worldToLocalMatrix * vanilla.transform.localToWorldMatrix;
                Bounds vb = vanilla.sharedMesh.bounds;
                Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
                if (w.IsShield) return ApplyShield(prefab, attach, vanilla, vanillaRenderer, m, vb, axes, dir, w);
                // vanilla haft axis (d) and blade-width axis (e) in attach space
                var order = Enumerable.Range(0, 3).OrderByDescending(i => vb.extents[i] * m.MultiplyVector(axes[i]).magnitude).ToArray();
                Vector3 a1 = axes[order[0]] * vb.extents[order[0]];
                Vector3 p1 = m.MultiplyPoint(vb.center + a1), p2 = m.MultiplyPoint(vb.center - a1);
                Vector3 head = p1.sqrMagnitude > p2.sqrMagnitude ? p1 : p2, tail = head == p1 ? p2 : p1;
                if (w.FlipHead) { var t0 = head; head = tail; tail = t0; }   // hand near the middle (spear): the farther end is not always the head
                Vector3 d = (head - tail).normalized;
                Vector3 e = Vector3.ProjectOnPlane(m.MultiplyVector(axes[order[1]]), d).normalized;
                // The hand is the attach origin. The vanilla bounds centre line can sit off the haft (an axe's blade
                // pulls it sideways), so only the direction comes from the vanilla model; the grip goes on the hand.
                Vector3 grip = Vector3.zero;
                float handAt = Vector3.Dot(-tail, d) / Vector3.Distance(head, tail);
                Plugin.Log.LogInfo(w.Prefab + ": vanilla " + vanilla.name + " is " + Vector3.Distance(head, tail).ToString("F2") + " m, hand at " + (handAt * 100f).ToString("F0") + "% from the butt");
                Quaternion rot = Quaternion.LookRotation(d, Vector3.Cross(d, e));
                if (w.FlipBlade) rot = Quaternion.AngleAxis(180f, d) * rot;

                var go = MakeModel(attach, vanilla, vanillaRenderer, dir, w, out Mesh mesh);
                go.transform.localPosition = grip;
                go.transform.localRotation = rot;

                Bounds b = mesh.bounds;   // model space: +Z runs from the grip to the head
                float headLen = b.size.z * w.HeadFraction;
                Vector3 headStart = new Vector3(b.center.x, b.center.y, b.max.z - headLen);
                Vector3 headEnd = new Vector3(b.center.x, b.center.y, b.max.z);

                Transform trail = attach.Find("equiped/trail");
                if (trail != null)
                {
                    SetWorld(trail.Find("base"), go.transform.TransformPoint(headStart));
                    SetWorld(trail.Find("tip"), go.transform.TransformPoint(headEnd));
                }
                FitCollider(attach, go, b.center, new Vector3(Mathf.Max(0.1f, b.size.x), Mathf.Max(0.05f, b.size.y), b.size.z));

                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning(w.Prefab + ": custom model not applied, using " + w.CloneFrom + " visuals: " + e.Message);
                return false;
            }
        }

        // The dropped item's collider, fitted to our model. Some bases (Bronze Spear) keep it on the vanilla mesh object,
        // which MakeModel hides: moving that object would leave the drop with no active collider and it falls through
        // the ground. Then a new collider goes on our model and the old one is removed.
        private static void FitCollider(Transform attach, GameObject go, Vector3 center, Vector3 size)
        {
            var box = attach.GetComponentsInChildren<BoxCollider>(true).FirstOrDefault();
            if (box == null)
            {   // e.g. the Iron Sledge: a MeshCollider on its model; if hiding the vanilla mesh took it out, give the drop a box
                bool live = attach.GetComponentsInChildren<Collider>(true).Any(c => !c.isTrigger && ActiveUnder(c.transform, attach));
                if (!live)
                {
                    var add = go.AddComponent<BoxCollider>();
                    add.center = center; add.size = size;
                }
                return;
            }
            if (box.GetComponent<MeshFilter>() != null || box.GetComponent<Renderer>() != null)
            {
                var nb = go.AddComponent<BoxCollider>();
                nb.sharedMaterial = box.sharedMaterial;
                UnityEngine.Object.DestroyImmediate(box);
                box = nb;
            }
            else
            {
                box.transform.SetParent(go.transform, false);
                box.transform.localPosition = Vector3.zero;
                box.transform.localRotation = Quaternion.identity;
                box.transform.localScale = Vector3.one;
            }
            box.center = center;
            box.size = size;
        }

        private static bool ActiveUnder(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent) if (!t.gameObject.activeSelf) return false;
            return true;
        }

        private static GameObject MakeModel(Transform attach, MeshFilter vanilla, MeshRenderer vanillaRenderer, string dir, WeaponDef w, out Mesh mesh)
        {
            mesh = BuildMesh(ObjMesh.Parse(File.ReadAllText(Path.Combine(dir, w.Asset + ".obj"))));
            // copy the game's own Custom/Creature material when the base has several (the Flint Spear's first one is a Standard-shader
            // flint material, which reads our normal map wrongly and makes the lighting blotchy)
            var baseMat = vanillaRenderer.sharedMaterials.FirstOrDefault(x => x != null && x.shader != null && x.shader.name == "Custom/Creature") ?? vanillaRenderer.sharedMaterial;
            var mat = new Material(baseMat) { name = "ftoath_" + w.Asset };
            mat.SetTexture("_MainTex", LoadTex(Path.Combine(dir, w.Asset + "_albedo.png"), false));
            mat.SetTexture("_BumpMap", LoadTex(Path.Combine(dir, w.Asset + "_normal.png"), true));
            mat.SetTexture("_MetallicGlossMap", LoadTex(Path.Combine(dir, w.Asset + "_metal.png"), true));
            mat.SetTexture("_EmissionMap", LoadTex(Path.Combine(dir, w.Asset + "_emission.png"), false));
            mat.SetColor("_Color", Color.white);
            mat.SetColor("_EmissionColor", Glow);
            mat.EnableKeyword("_EMISSION");
            mat.EnableKeyword("_NORMALMAP");
            mat.EnableKeyword("_METALLICGLOSSMAP");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            var go = new GameObject("ftoath_" + w.Asset) { layer = vanilla.gameObject.layer };
            go.transform.SetParent(attach, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            vanilla.gameObject.SetActive(false);
            // some bases are built from several meshes (Stagbreaker's antler cage); hide every other vanilla renderer too
            foreach (var r in attach.GetComponentsInChildren<Renderer>(true))
                if (r.gameObject != go && (r is MeshRenderer || r is SkinnedMeshRenderer) && !Under(r.transform, "UpgraderGlow") && !Under(r.transform, "equiped"))
                    r.enabled = false;
            return go;
        }

        // Round shield: the model's thickness axis (+Y = back with the handle, -Y = rune face) goes onto the vanilla
        // shield's thinnest axis with the back toward the hand; centre on the vanilla centre; scale to its diameter.
        private static bool ApplyShield(GameObject prefab, Transform attach, MeshFilter vanilla, MeshRenderer vanillaRenderer, Matrix4x4 m, Bounds vb, Vector3[] axes, string dir, WeaponDef w)
        {
            var order = Enumerable.Range(0, 3).OrderBy(i => vb.extents[i] * m.MultiplyVector(axes[i]).magnitude).ToArray();
            Vector3 n = m.MultiplyVector(axes[order[0]]).normalized;
            Vector3 c = m.MultiplyPoint(vb.center);
            Vector3 back = Vector3.Dot(-c, n) >= 0 ? n : -n;              // the hand is behind the shield
            Vector3 up = Vector3.ProjectOnPlane(m.MultiplyVector(axes[order[2]]), back).normalized;
            float vanillaDiameter = 2f * Mathf.Max(vb.extents[order[1]] * m.MultiplyVector(axes[order[1]]).magnitude, vb.extents[order[2]] * m.MultiplyVector(axes[order[2]]).magnitude);
            if (w.FlipBlade) up = -up;
            if (w.ShieldRoll != 0f) up = Quaternion.AngleAxis(w.ShieldRoll, back) * up;   // turn the face in its own plane

            var go = MakeModel(attach, vanilla, vanillaRenderer, dir, w, out Mesh mesh);
            Bounds b = mesh.bounds;
            float k = vanillaDiameter / Mathf.Max(b.size.x, b.size.z);
            Quaternion rot = Quaternion.LookRotation(up, back);          // model +Z -> up, model +Y -> back
            go.transform.localRotation = rot;
            go.transform.localScale = Vector3.one * k;
            go.transform.localPosition = c - rot * (b.center * k);
            Plugin.Log.LogInfo(w.Prefab + ": vanilla shield " + vanillaDiameter.ToString("F2") + " m across, model scaled x" + k.ToString("F2"));

            FitCollider(attach, go, b.center, b.size);
            return true;
        }

        // Thrown projectile: hide the vanilla model and show the item's custom model, pointing the way the projectile
        // flies (+Z) with its tip on the vanilla tip. Same size as in the hand.
        internal static bool ApplyProjectile(GameObject proj, GameObject item, WeaponDef w)
        {
            try
            {
                var ours = item.transform.Find("attach")?.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name.StartsWith("ftoath_"))
                           ?? throw new Exception("item has no custom model");
                var vanilla = proj.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null && f.GetComponent<MeshRenderer>() != null)
                    .OrderByDescending(f => f.sharedMesh.bounds.size.sqrMagnitude * f.transform.lossyScale.sqrMagnitude).FirstOrDefault()
                    ?? throw new Exception("no vanilla mesh on the projectile");
                Matrix4x4 m = proj.transform.worldToLocalMatrix * vanilla.transform.localToWorldMatrix;
                Bounds vb = vanilla.sharedMesh.bounds;
                Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
                var order = Enumerable.Range(0, 3).OrderByDescending(i => vb.extents[i] * m.MultiplyVector(axes[i]).magnitude).ToArray();
                Vector3 a1 = axes[order[0]] * vb.extents[order[0]];
                Vector3 p1 = m.MultiplyPoint(vb.center + a1), p2 = m.MultiplyPoint(vb.center - a1);
                Vector3 tip = p1.z > p2.z ? p1 : p2, tail = tip == p1 ? p2 : p1;
                Vector3 d = (tip - tail).normalized;
                Vector3 e = Vector3.ProjectOnPlane(m.MultiplyVector(axes[order[1]]), d).normalized;
                Quaternion rot = Quaternion.LookRotation(d, Vector3.Cross(d, e));
                if (w.FlipBlade) rot = Quaternion.AngleAxis(180f, d) * rot;

                var go = new GameObject("ftoath_projectile_" + w.Asset) { layer = vanilla.gameObject.layer };
                go.transform.SetParent(proj.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = ours.sharedMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = ours.GetComponent<MeshRenderer>().sharedMaterial;
                go.transform.localRotation = rot;
                go.transform.localPosition = tip - rot * new Vector3(0f, 0f, ours.sharedMesh.bounds.max.z);
                foreach (var r in proj.GetComponentsInChildren<Renderer>(true))
                    if (r.gameObject != go && (r is MeshRenderer || r is SkinnedMeshRenderer)) r.enabled = false;
                Plugin.Log.LogInfo(w.Prefab + ": projectile model " + Vector3.Distance(tip, tail).ToString("F2") + " m vanilla, ours " + ours.sharedMesh.bounds.size.z.ToString("F2") + " m");
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning(w.Prefab + ": thrown model not applied, the vanilla projectile is used: " + e.Message);
                return false;
            }
        }

        // Rotation for the inventory icon: haft laid diagonally, blade flat to the camera.
        internal static Quaternion IconRotation(GameObject prefab)
        {
            Transform model = prefab.transform.Find("attach")?.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name.StartsWith("ftoath_"))?.transform;
            if (model == null) return Quaternion.identity;
            Quaternion inRoot = Quaternion.Inverse(prefab.transform.rotation) * model.rotation;
            if (model.name.EndsWith("_shield"))
            {   // face to the camera, upright
                Vector3 front = inRoot * Vector3.down, upAxis = inRoot * Vector3.forward;
                return Quaternion.LookRotation(Vector3.forward, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(front, upAxis));
            }
            Vector3 d = inRoot * Vector3.forward, e = inRoot * Vector3.right;
            Vector3 t = new Vector3(0.7071f, 0.7071f, 0f), tPerp = new Vector3(-0.7071f, 0.7071f, 0f);
            Quaternion from = Quaternion.LookRotation(d, Vector3.Cross(d, e));
            Quaternion to = Quaternion.LookRotation(t, Vector3.Cross(t, tPerp));
            return to * Quaternion.Inverse(from);
        }

        private static bool Under(Transform t, string name)
        {
            for (var p = t; p != null; p = p.parent) if (p.name == name) return true;
            return false;
        }

        private static void SetWorld(Transform t, Vector3 world)
        {
            if (t != null) t.position = world;
        }

        private static Mesh BuildMesh(ObjMesh m)
        {
            int n = m.Positions.Length / 3;
            var v = new Vector3[n]; var nr = new Vector3[n]; var uv = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                v[i] = new Vector3(m.Positions[i * 3], m.Positions[i * 3 + 1], m.Positions[i * 3 + 2]);
                nr[i] = new Vector3(m.Normals[i * 3], m.Normals[i * 3 + 1], m.Normals[i * 3 + 2]);
                uv[i] = new Vector2(m.Uvs[i * 2], m.Uvs[i * 2 + 1]);
            }
            var mesh = new Mesh { name = "ftoath" };
            if (n > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = v; mesh.normals = nr; mesh.uv = uv;
            mesh.triangles = m.Triangles;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Texture2D LoadTex(string path, bool linear)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear) { name = Path.GetFileNameWithoutExtension(path) };
            if (!tex.LoadImage(File.ReadAllBytes(path))) throw new Exception("bad image " + path);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.anisoLevel = 4;
            return tex;
        }
    }
}
