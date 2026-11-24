using System.Collections.Generic;
using UnityEngine;

namespace PotMerge
{
    /// <summary>The hamster's smile: the round eyes are swapped for happy arcs and a smiling mouth is shown.</summary>
    public partial class ShopOwner
    {
        GameObject smile;
        bool smiling;
        float swing; // 0..1: how much the smiling hamster sways from side to side

        void UpdateSmile()
        {
            if (smileT > 0f) smileT -= Time.deltaTime;
            bool on = smileT > 0f;
            swing = Mathf.MoveTowards(swing, on ? 1f : 0f, Time.deltaTime * 4f);
            if (on == smiling || model == null) return;
            smiling = on;
            if (smile == null) BuildSmile();
            smile.SetActive(on);
            foreach (var e in eyes) if (e != null) e.GetComponent<Renderer>().enabled = !on;
        }

        /// <summary>Built in the hamster model's own space (meters, Z up, front = -Y).</summary>
        void BuildSmile()
        {
            smile = new GameObject("Smile");
            smile.transform.SetParent(model.transform, false);
            Material ink = null;
            foreach (var e in eyes)
                if (e != null && !e.name.Contains("Shine")) { ink = e.GetComponent<Renderer>().sharedMaterial; break; }
            foreach (float s in new[] { -1f, 1f })
                Arc(new Vector3(s * 0.061f, -0.176f, 0.396f), 0.027f, 0.024f, 15f, 165f, 0.0055f, ink);
            foreach (float s in new[] { -1f, 1f }) // a little "w" mouth on each side of the teeth
                Arc(new Vector3(s * 0.024f, -0.190f, 0.338f), 0.024f, 0.018f, 180f, 360f, 0.0042f, ink);
        }

        void Arc(Vector3 center, float rx, float rz, float from, float to, float radius, Material mat)
        {
            const int N = 14, K = 8;
            var v = new List<Vector3>();
            var tri = new List<int>();
            for (int i = 0; i <= N; i++)
            {
                float a = Mathf.Lerp(from, to, i / (float)N) * Mathf.Deg2Rad;
                var p = center + new Vector3(Mathf.Cos(a) * rx, 0f, Mathf.Sin(a) * rz);
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float r = radius * (i == 0 || i == N ? 0.6f : 1f);
                for (int k = 0; k < K; k++)
                {
                    float b = k * 2f * Mathf.PI / K;
                    v.Add(p + r * (Mathf.Cos(b) * outward + Mathf.Sin(b) * Vector3.up));
                }
            }
            for (int i = 0; i < N; i++)
                for (int k = 0; k < K; k++)
                {
                    int k1 = (k + 1) % K, a0 = i * K + k, a1 = i * K + k1, b0 = a0 + K, b1 = a1 + K;
                    tri.AddRange(new[] { a0, a1, b1, a0, b1, b0 });
                }
            var mesh = new Mesh { name = "SmileArc" };
            mesh.SetVertices(v);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals();
            var go = new GameObject("Arc");
            go.transform.SetParent(smile.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            // no shadow: the arcs float just off the face and their shadow looked like dark circles under the eyes
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
    }
}
