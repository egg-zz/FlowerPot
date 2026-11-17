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
        [Tooltip("Shop model for the title/home. Empty = the Flower chain's level 11 shop.")]
        public GameObject heroModel;
        public float shopSize = 6f;
        public float islandSize = 11f;
        public Color sky = new Color(0.80f, 0.91f, 0.99f);
        public Color grass = new Color(0.55f, 0.82f, 0.45f);
        public Color soil = new Color(0.72f, 0.52f, 0.38f);
        public Color path = new Color(0.98f, 0.86f, 0.70f);

        [Header("Camera")]
        public Vector3 cameraLocalPos = new Vector3(0f, 6f, -17f);
        public float cameraPitch = 12f;
        public float cameraFov = 50f;
        public float swayDegrees = 5f;

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

            // one cute shop on a soft round island floating in the sky
            var island = RoundedMesh.Create("Island", root, islandSize, islandSize, 0.8f, islandSize * 0.5f, 0.35f, grass);
            island.transform.localPosition = new Vector3(0f, -0.4f, 0f);
            var under = RoundedMesh.Create("IslandSoil", root, islandSize * 0.92f, islandSize * 0.92f, 0.9f, islandSize * 0.46f, 0.3f, soil);
            under.transform.localPosition = new Vector3(0f, -1.1f, 0f);
            var mat = RoundedMesh.Create("DoorMat", root, 2.2f, 2.6f, 0.06f, 1.0f, 0.02f, path);
            mat.transform.localPosition = new Vector3(0f, 0.02f, -2.6f);

            var holder = new GameObject("Shop").transform;
            holder.SetParent(root, false);
            holder.localPosition = new Vector3(0f, 0f, 0.4f);
            var model = heroModel != null ? heroModel : (database.IsValid(chain, 11) ? database.Model(chain, 11) : null);
            if (model != null) ModelUtil.Spawn(model, holder, shopSize, shopSize);

            root.gameObject.SetActive(showing);
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
            cam.transform.position = offset + Quaternion.Euler(0f, yaw, 0f) * cameraLocalPos;
            cam.transform.rotation = rot;
        }
    }
}
