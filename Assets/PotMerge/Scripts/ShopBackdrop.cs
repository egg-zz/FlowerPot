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
        [Tooltip("Home screen framing: camera a bit higher than the title so the shop sits below the wallet/hearts pills and above the LEVEL button")]
        public Vector3 homeCameraLocalPos = new Vector3(0f, 7.0f, -17f);
        public float homeCameraPitch = 16.5f;
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

        /// <summary>Sideways camera offset while moving to or from the town.</summary>
        public float Pan { get; set; }

        float homeBlend, homeTarget;

        /// <summary>Takes over the camera and shows the diorama (title framing, or home framing).</summary>
        public void Show(Camera camera, bool homeFraming = false)
        {
            homeTarget = homeFraming ? 1f : 0f;
            if (!showing) homeBlend = homeTarget;
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
                cam.fieldOfView = FovForAspect();
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

        /// <summary>Phones taller than 9:16: widen the vertical FOV so the 9:16 width (shop + hamster) always stays on screen.</summary>
        float FovForAspect()
        {
            float k = Mathf.Max(1f, (9f / 16f) / cam.aspect);
            return 2f * Mathf.Rad2Deg * Mathf.Atan(Mathf.Tan(cameraFov * 0.5f * Mathf.Deg2Rad) * k);
        }

        void LateUpdate()
        {
            if (showing) ApplyCamera(Time.time);
        }

        void ApplyCamera(float t)
        {
            if (cam == null) return;
            homeBlend = Mathf.MoveTowards(homeBlend, homeTarget, Time.deltaTime * 2.5f);
            float k = Mathf.SmoothStep(0f, 1f, homeBlend);
            float pitch = Mathf.Lerp(cameraPitch, homeCameraPitch, k);
            Vector3 local = Vector3.Lerp(cameraLocalPos, homeCameraLocalPos, k);
            cam.fieldOfView = FovForAspect();
            float yaw = Mathf.Sin(t * 0.35f) * swayDegrees;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            cam.transform.position = offset + Quaternion.Euler(0f, yaw, 0f) * local + Vector3.right * Pan;
            cam.transform.rotation = rot;
        }
    }
}
