using UnityEngine;

namespace PotMerge
{
    public static class ModelUtil
    {
        /// <summary>
        /// Instantiates a model under <paramref name="parent"/>, fitted into a box (footprint x height),
        /// standing on the parent's origin and centered on it.
        /// - The FBX root keeps its own rotation (axis conversion from 3ds Max). Never overwrite it.
        /// - 3ds Max front (-Y) arrives facing +Z, so the model is turned 180 degrees to face the camera.
        /// </summary>
        public static GameObject Spawn(GameObject prefab, Transform parent, float footprint, float height,
            float sizeMul = 1f, bool turnToCamera = true)
        {
            var fit = new GameObject("Fit").transform;
            fit.SetParent(parent, false);

            var model = Object.Instantiate(prefab, fit, false);
            foreach (var c in model.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            if (turnToCamera) model.transform.Rotate(0f, 180f, 0f, Space.World);

            if (!LocalBounds(model, fit, out var b)) return model;

            float horiz = Mathf.Max(b.size.x, b.size.z, 0.0001f);
            float scale = Mathf.Min(footprint / horiz, height / Mathf.Max(b.size.y, 0.0001f)) * sizeMul;

            // ground + center (in fit space, before scaling), then scale the fit node
            model.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
            fit.localScale = Vector3.one * scale;
            return model;
        }

        /// <summary>Renderer bounds of <paramref name="go"/> expressed in <paramref name="space"/>'s local space (axis aligned).</summary>
        public static bool LocalBounds(GameObject go, Transform space, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var wb = r.bounds;
                Vector3 mn = wb.min, mx = wb.max;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? mn.x : mx.x, (i & 2) == 0 ? mn.y : mx.y, (i & 4) == 0 ? mn.z : mx.z);
                    var p = space.InverseTransformPoint(corner);
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return any;
        }
    }
}
