using UnityEngine;

// Standalone play-mode entry point while Tower and Battle are being ported separately.
public sealed class BattleSandboxLauncher : MonoBehaviour
{
    [SerializeField] private int towerFloor;
    private BattleMode mode;
    private bool restart;

    private void Start()
    {
        if (Camera.main == null)
        {
            GameObject cameraObject = new GameObject("Battle Sandbox Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .044f, .075f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
        }
        Application.runInBackground = true;
        mode = GetComponent<BattleMode>();
        if (mode == null) mode = gameObject.AddComponent<BattleMode>();
        StartFight();
    }

    private void Update()
    {
        if (!restart) return;
        restart = false;
        StartFight();
    }

    private void StartFight() { mode.Begin(towerFloor, OnFightLeft); }

    private void OnFightLeft(bool victory, int gold)
    {
        Debug.Log("Battle Sandbox: " + (victory ? "victory" : "ended") + ", reward " + gold
            + ". Restarting floor " + towerFloor + ".");
        restart = true;
    }
}
