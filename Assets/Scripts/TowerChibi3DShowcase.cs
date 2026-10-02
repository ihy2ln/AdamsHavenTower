using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Play-mode review of the Tower 3D chibis: all eight in a row, each cycling through the room work loops.
// Left/Right arrows step every chibi to the previous/next room; Space pauses the auto cycle.
public sealed class TowerChibi3DShowcase : MonoBehaviour
{
    static readonly string[] Rooms =
        { "kitchen", "forge", "well", "farmstead", "market", "warehouse", "lumber_mill", "quarry", "frosted_mug", "heart", "gate", "deck_hall", "house", "barn" };
    public float SecondsPerRoom = 5f, Spacing = 1.25f, Height = 1.6f;
    readonly List<TowerChibi3D> chibis = new List<TowerChibi3D>();
    int room;
    float timer;
    bool paused;

    void Start()
    {
        for (int i = 0; i < TowerChibi3D.Ids.Length; i++)
        {
            var slot = new GameObject("slot " + TowerChibi3D.Ids[i]).transform;
            slot.SetParent(transform, false);
            slot.localPosition = new Vector3((i - (TowerChibi3D.Ids.Length - 1) / 2f) * Spacing, 0, 0);
            slot.localRotation = Quaternion.Euler(0, 20, 0);
            var c = TowerChibi3D.Spawn(TowerChibi3D.Ids[i], slot, Height);
            if (c) chibis.Add(c);
        }
        Apply();
    }

    void Apply() { foreach (var c in chibis) c.PlayRoom(Rooms[room]); timer = 0; }

    void Update()
    {
        var keys = Keyboard.current;
        if (keys != null)
        {
            if (keys.spaceKey.wasPressedThisFrame) paused = !paused;
            if (keys.rightArrowKey.wasPressedThisFrame) { room = (room + 1) % Rooms.Length; Apply(); }
            if (keys.leftArrowKey.wasPressedThisFrame) { room = (room + Rooms.Length - 1) % Rooms.Length; Apply(); }
        }
        if (paused) return;
        timer += Time.deltaTime;
        if (timer >= SecondsPerRoom) { room = (room + 1) % Rooms.Length; Apply(); }
    }

    void OnGUI()
    {
        string clip = chibis.Count > 0 ? chibis[0].Clip : "";
        GUI.Label(new Rect(12, 10, 600, 24), "Room: " + Rooms[room] + "  (" + clip + ")   ←/→ change, Space pause");
    }
}
