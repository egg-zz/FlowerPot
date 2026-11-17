using UnityEngine;

namespace PotMerge
{
    /// <summary>
    /// Shows the hamster owner (with the equipped costumes) at the top center of the game screen.
    /// The hamster stands on a hidden stage far below the board and a small camera renders it into a texture
    /// that the HUD displays. Every merge makes the hamster jump for joy.
    /// </summary>
    public class OwnerPortrait : MonoBehaviour
    {
        public CostumeDatabase costumes;
        public MergeBoard board;
        public MergeHUD hud;
        public Vector3 stagePosition = new Vector3(0f, -300f, 0f);
        public int textureSize = 512;

        ShopOwner owner;
        Camera cam;
        RenderTexture rt;

        void Start()
        {
            if (costumes == null || costumes.hamsterModel == null || hud == null) { enabled = false; return; }

            var go = new GameObject("PortraitOwner");
            go.transform.SetParent(transform, false);
            owner = go.AddComponent<ShopOwner>();
            owner.costumes = costumes;
            owner.position = stagePosition;
            owner.yaw = 0f;
            owner.height = 2.4f;

            rt = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "OwnerPortrait" };
            cam = new GameObject("PortraitCamera").AddComponent<Camera>();
            cam.transform.SetParent(transform, false);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 34f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 30f;
            cam.transform.position = stagePosition + new Vector3(0f, 2.5f, -6.8f);
            cam.transform.LookAt(stagePosition + new Vector3(0f, 1.9f, 0f));
            cam.targetTexture = rt;
            cam.depth = -10f;

            hud.SetOwnerPortrait(rt);
            if (board != null) board.Merged += OnMerged;
        }

        void OnDestroy()
        {
            if (board != null) board.Merged -= OnMerged;
            if (rt != null) rt.Release();
        }

        void OnMerged(int chain, int newLevel) => owner?.Cheer(newLevel);

        void Update()
        {
            // only render while the game screen is up
            if (cam != null && board != null) cam.enabled = board.Active;
        }
    }
}
