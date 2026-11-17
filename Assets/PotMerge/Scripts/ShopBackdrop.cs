using UnityEngine;

namespace PotMerge
{
    /// <summary>
    /// Flower-shop diorama shown behind the title and home screens. Built at runtime from the Flower chain models,
    /// placed away from the board so the game camera never sees it.
    /// </summary>
    public class ShopBackdrop : MonoBehaviour
    {
        public ChainDatabase database;
        public int chain = 0; // Flower
        public Vector3 offset = new Vector3(80f, 0f, 0f);
        public Color sky = new Color(0.78f, 0.90f, 0.98f);
        public Color grass = new Color(0.42f, 0.68f, 0.33f);
        public Color path = new Color(0.86f, 0.72f, 0.56f);

        [Header("Camera")]
        public Vector3 cameraLocalPos = new Vector3(0f, 4.2f, -19f);
        public float cameraPitch = 3f;
        public float cameraFov = 50f;
        public float swayDegrees = 2.5f;

        Transform root;
        Camera cam;
        bool showing;
        Color savedBg;
        CameraClearFlags savedFlags;
        float savedFov;

        void Start() => Build();

        void Build()
        {
            if (root != null || database == null) return;
            root = new GameObject("ShopBackdropSet").transform;
            root.SetParent(transform, false);
            root.position = offset;

            Ground("Grass", new Vector3(0f, -0.05f, 6f), new Vector3(60f, 0.1f, 40f), grass);
            Ground("Path", new Vector3(0f, -0.02f, -2f), new Vector3(2.6f, 0.1f, 12f), path);
            Ground("Plaza", new Vector3(0f, -0.03f, 2.6f), new Vector3(11f, 0.1f, 3.4f), path);

            // shop in the back, stall on the left, plants along the path
            Item(11, new Vector3(0f, 0f, 5.5f), 7f, 6.8f);
            Item(10, new Vector3(-5.6f, 0f, 3.8f), 3.4f, 3.6f);
            Item(9, new Vector3(5.2f, 0f, 3.6f), 1.5f, 2.0f);
            Item(7, new Vector3(6.0f, 0f, 1.6f), 1.2f, 1.6f);
            Item(6, new Vector3(-2.5f, 0f, 1.2f), 1.0f, 1.4f);
            Item(6, new Vector3(2.5f, 0f, 1.2f), 1.0f, 1.4f);
            Item(4, new Vector3(-2.2f, 0f, -2.6f), 0.8f, 1.0f);
            Item(5, new Vector3(2.2f, 0f, -2.6f), 0.8f, 1.1f);
            Item(8, new Vector3(-5.4f, 0f, 0.8f), 1.1f, 1.6f);
            Item(3, new Vector3(-4.0f, 0f, -1.4f), 0.8f, 0.9f);
            Item(2, new Vector3(4.0f, 0f, -1.4f), 0.8f, 0.8f);

            root.gameObject.SetActive(showing);
        }

        void Ground(string name, Vector3 pos, Vector3 size, Color c)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(root, false);
            g.transform.localPosition = pos;
            g.transform.localScale = size;
            g.GetComponent<Renderer>().material.color = c;
        }

        void Item(int level, Vector3 pos, float footprint, float height)
        {
            if (!database.IsValid(chain, level)) return;
            var holder = new GameObject($"{database.Get(chain).id}_L{level}").transform;
            holder.SetParent(root, false);
            holder.localPosition = pos;
            ModelUtil.Spawn(database.Model(chain, level), holder, footprint, height);
        }

        /// <summary>Takes over the camera and shows the diorama.</summary>
        public void Show(Camera camera)
        {
            Build();
            if (!showing && camera != null)
            {
                savedBg = camera.backgroundColor;
                savedFlags = camera.clearFlags;
                savedFov = camera.fieldOfView;
            }
            cam = camera;
            showing = true;
            if (root != null) root.gameObject.SetActive(true);
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = sky;
                cam.fieldOfView = cameraFov;
            }
            ApplyCamera(0f);
        }

        /// <summary>Hides the diorama and gives the camera back (colors/FOV restored).</summary>
        public void Hide()
        {
            if (showing && cam != null)
            {
                cam.backgroundColor = savedBg;
                cam.clearFlags = savedFlags;
                cam.fieldOfView = savedFov;
            }
            showing = false;
            if (root != null) root.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (showing) ApplyCamera(Time.time);
        }

        void ApplyCamera(float t)
        {
            if (cam == null) return;
            float yaw = Mathf.Sin(t * 0.35f) * swayDegrees;
            var rot = Quaternion.Euler(cameraPitch, yaw, 0f);
            Vector3 pivot = offset + new Vector3(0f, 0f, 3f);
            Vector3 local = cameraLocalPos - new Vector3(0f, 0f, 3f);
            cam.transform.position = pivot + Quaternion.Euler(0f, yaw, 0f) * local;
            cam.transform.rotation = rot;
        }
    }
}
