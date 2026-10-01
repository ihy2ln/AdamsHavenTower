using UnityEngine;
using UnityEngine.UI;

// UI playback of the same rig-baked chibi atlases used by the Tower.
public sealed class TowerDungeonPortrait : MonoBehaviour
{
    private RawImage image;
    private Texture2D idle, walk;
    private bool travelling;
    private float seconds;

    public bool Configure(string unit)
    {
        image = GetComponent<RawImage>();
        idle = Resources.Load<Texture2D>("AdamsHaven/ChibiMotion/" + unit + "/idle");
        walk = Resources.Load<Texture2D>("AdamsHaven/ChibiMotion/" + unit + "/walk_in_place");
        if (idle == null) return false;
        SetTravelling(false);
        return true;
    }

    public void SetTravelling(bool value)
    {
        if (travelling != value) seconds = 0;
        travelling = value;
        if (idle == null || image == null) return;
        image.texture = travelling && walk != null ? walk : idle;
        ShowFrame();
    }

    private void Update()
    {
        if (idle == null) return;
        seconds += Time.unscaledDeltaTime;
        ShowFrame();
    }

    private void ShowFrame()
    {
        int frame = Mathf.FloorToInt(seconds * 12) % 24;
        image.uvRect = new Rect((frame % 8 * 128 + 2) / 1024f,
            1 - (frame / 8 * 192 + 3 + 186) / 1024f, 124 / 1024f, 186 / 1024f);
    }
}
