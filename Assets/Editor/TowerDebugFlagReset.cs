using AdamsHaven.Tower;
using UnityEditor;

// The HUD's debug "expedition map" shortcut sets TowerRules.DebugIgnoreGuild. Statics survive leaving Play mode in the
// editor, which made the Guild tests fail afterwards. Clear the flag whenever Play mode ends or starts.
[InitializeOnLoad]
public static class TowerDebugFlagReset
{
    static TowerDebugFlagReset()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode ||
                state == PlayModeStateChange.ExitingEditMode)
                TowerRules.DebugIgnoreGuild = false;
        };
    }
}
