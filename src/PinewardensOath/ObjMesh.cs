using System;
using System.Collections.Generic;
using System.Globalization;

namespace PinewardensOath
{
    // Minimal Wavefront OBJ reader (v/vt/vn/f). Produces flat arrays ready for UnityEngine.Mesh.
    // OBJ is right-handed; Unity is left-handed, so X is negated and triangle winding reversed.
    public sealed class ObjMesh
    {
        public float[] Positions, Normals, Uvs;
        public int[] Triangles;

        public static ObjMesh Parse(string text)
        {
            var v = new List<float>(); var vt = new List<float>(); var vn = new List<float>();
            var pos = new List<float>(); var nrm = new List<float>(); var uv = new List<float>();
            var tris = new List<int>();
            var map = new Dictionary<(int, int, int), int>();
            var lines = text.Split('\n');
            for (int li = 0; li < lines.Length; li++)
            {
                string line = lines[li].Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] p = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                switch (p[0])
                {
                    case "v": Add(v, p, 3, li); break;
                    case "vt": Add(vt, p, 2, li); break;
                    case "vn": Add(vn, p, 3, li); break;
                    case "f":
                        if (p.Length < 4) throw Bad(li, "face needs at least 3 corners");
                        var corner = new int[p.Length - 1];
                        for (int c = 1; c < p.Length; c++)
                        {
                            var key = Corner(p[c], v.Count / 3, vt.Count / 2, vn.Count / 3, li);
                            if (!map.TryGetValue(key, out int idx))
                            {
                                idx = pos.Count / 3; map[key] = idx;
                                pos.Add(-v[key.Item1 * 3]); pos.Add(v[key.Item1 * 3 + 1]); pos.Add(v[key.Item1 * 3 + 2]);
                                uv.Add(vt[key.Item2 * 2]); uv.Add(vt[key.Item2 * 2 + 1]);
                                nrm.Add(-vn[key.Item3 * 3]); nrm.Add(vn[key.Item3 * 3 + 1]); nrm.Add(vn[key.Item3 * 3 + 2]);
                            }
                            corner[c - 1] = idx;
                        }
                        for (int k = 1; k + 1 < corner.Length; k++) { tris.Add(corner[0]); tris.Add(corner[k + 1]); tris.Add(corner[k]); }
                        break;
                }
            }
            if (tris.Count == 0) throw new FormatException("OBJ has no faces");
            return new ObjMesh { Positions = pos.ToArray(), Normals = nrm.ToArray(), Uvs = uv.ToArray(), Triangles = tris.ToArray() };
        }

        private static void Add(List<float> list, string[] p, int n, int li)
        {
            if (p.Length < n + 1) throw Bad(li, "expected " + n + " numbers");
            for (int i = 1; i <= n; i++)
            {
                if (!float.TryParse(p[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) throw Bad(li, "bad number '" + p[i] + "'");
                list.Add(f);
            }
        }

        private static (int, int, int) Corner(string s, int nv, int nvt, int nvn, int li)
        {
            string[] q = s.Split('/');
            if (q.Length != 3 || q[1].Length == 0 || q[2].Length == 0) throw Bad(li, "face corner '" + s + "' needs v/vt/vn (UVs and normals required)");
            return (Index(q[0], nv, li), Index(q[1], nvt, li), Index(q[2], nvn, li));
        }

        private static int Index(string s, int count, int li)
        {
            if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) throw Bad(li, "bad index '" + s + "'");
            int r = i < 0 ? count + i : i - 1;
            if (r < 0 || r >= count) throw Bad(li, "index " + i + " out of range");
            return r;
        }

        private static FormatException Bad(int li, string msg) => new FormatException("OBJ line " + (li + 1) + ": " + msg);
    }
}
