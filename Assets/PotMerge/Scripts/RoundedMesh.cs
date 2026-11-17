using System.Collections.Generic;
using UnityEngine;

namespace PotMerge
{
    /// <summary>Procedural meshes with soft, rounded shapes (board tiles, slab, islands).</summary>
    public static class RoundedMesh
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        /// <summary>
        /// Box with rounded corners in X/Z and a rounded (beveled) top edge. Origin at the center of the box.
        /// </summary>
        public static Mesh Box(float width, float depth, float height, float cornerRadius, float topBevel = 0f, int cornerSegments = 8, int bevelSegments = 3)
        {
            string key = $"{width:F3}_{depth:F3}_{height:F3}_{cornerRadius:F3}_{topBevel:F3}_{cornerSegments}_{bevelSegments}";
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            cornerRadius = Mathf.Clamp(cornerRadius, 0.001f, Mathf.Min(width, depth) * 0.5f);
            topBevel = Mathf.Clamp(topBevel, 0f, Mathf.Min(cornerRadius * 0.95f, height * 0.9f));
            if (topBevel < 0.0001f) bevelSegments = 0;

            // outline of the rounded rectangle (counter-clockwise seen from above)
            var outline = new List<Vector2>();
            var normals2 = new List<Vector2>();
            float hx = width * 0.5f - cornerRadius, hz = depth * 0.5f - cornerRadius;
            Vector2[] centers = { new Vector2(hx, hz), new Vector2(-hx, hz), new Vector2(-hx, -hz), new Vector2(hx, -hz) };
            for (int c = 0; c < 4; c++)
            {
                for (int i = 0; i <= cornerSegments; i++)
                {
                    float a = (c * 90f + i * 90f / cornerSegments) * Mathf.Deg2Rad;
                    var n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    outline.Add(centers[c] + n * cornerRadius);
                    normals2.Add(n);
                }
            }
            int ring = outline.Count;

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            float top = height * 0.5f, bottom = -height * 0.5f;

            // rings from bottom to top: bottom edge, start of bevel, bevel steps (inset), top
            var ringY = new List<float>();
            var ringInset = new List<float>();
            var ringNormalUp = new List<float>();
            ringY.Add(bottom); ringInset.Add(0f); ringNormalUp.Add(0f);
            ringY.Add(top - topBevel); ringInset.Add(0f); ringNormalUp.Add(0f);
            for (int b = 1; b <= bevelSegments; b++)
            {
                float a = b / (float)bevelSegments * Mathf.PI * 0.5f;
                ringY.Add(top - topBevel + Mathf.Sin(a) * topBevel);
                ringInset.Add(topBevel - Mathf.Cos(a) * topBevel);
                ringNormalUp.Add(a);
            }

            // side rings (separate vertices per ring for smooth bevel normals)
            for (int r = 0; r < ringY.Count; r++)
            {
                float ang = ringNormalUp[r];
                for (int i = 0; i < ring; i++)
                {
                    var n2 = normals2[i];
                    var p2 = outline[i] - n2 * ringInset[r];
                    verts.Add(new Vector3(p2.x, ringY[r], p2.y));
                    norms.Add(new Vector3(n2.x * Mathf.Cos(ang), Mathf.Sin(ang), n2.y * Mathf.Cos(ang)).normalized);
                }
            }
            for (int r = 0; r < ringY.Count - 1; r++)
            {
                int a0 = r * ring, b0 = (r + 1) * ring;
                for (int i = 0; i < ring; i++)
                {
                    int i1 = (i + 1) % ring;
                    tris.Add(a0 + i); tris.Add(b0 + i); tris.Add(a0 + i1);
                    tris.Add(a0 + i1); tris.Add(b0 + i); tris.Add(b0 + i1);
                }
            }

            // top cap (fan)
            float lastInset = ringInset[ringInset.Count - 1];
            int topCenter = verts.Count;
            verts.Add(new Vector3(0f, top, 0f)); norms.Add(Vector3.up);
            for (int i = 0; i < ring; i++)
            {
                var p2 = outline[i] - normals2[i] * lastInset;
                verts.Add(new Vector3(p2.x, top, p2.y)); norms.Add(Vector3.up);
            }
            for (int i = 0; i < ring; i++)
            {
                int i1 = (i + 1) % ring;
                tris.Add(topCenter); tris.Add(topCenter + 1 + i1); tris.Add(topCenter + 1 + i);
            }

            // bottom cap
            int botCenter = verts.Count;
            verts.Add(new Vector3(0f, bottom, 0f)); norms.Add(Vector3.down);
            for (int i = 0; i < ring; i++) { verts.Add(new Vector3(outline[i].x, bottom, outline[i].y)); norms.Add(Vector3.down); }
            for (int i = 0; i < ring; i++)
            {
                int i1 = (i + 1) % ring;
                tris.Add(botCenter); tris.Add(botCenter + 1 + i); tris.Add(botCenter + 1 + i1);
            }

            var mesh = new Mesh { name = "RoundedBox_" + key };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            cache[key] = mesh;
            return mesh;
        }

        /// <summary>Creates a GameObject with a rounded box mesh and a colored Standard material.</summary>
        public static GameObject Create(string name, Transform parent, float width, float depth, float height, float cornerRadius, float topBevel, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Box(width, depth, height, cornerRadius, topBevel);
            var mr = go.AddComponent<MeshRenderer>();
            mr.material = new Material(Shader.Find("Standard")) { color = color };
            mr.material.SetFloat("_Glossiness", 0.15f);
            return go;
        }
    }
}
