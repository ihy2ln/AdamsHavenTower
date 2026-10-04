using UnityEngine;
using UnityEngine.SceneManagement;

namespace AdamsHaven.Tower
{
    // Dungeon Mode fork (TT 10.4.0, TOWER_MODE_GDD 20). The game runs as the Tower or as the Dungeon; the choice lives in
    // a PlayerPref, and each mode keeps its saves in its own folder. A save also records its own mode (TowerState.mode),
    // so rules code never reads this static: only the save folder, the checkpoint generator and the HUD do.
    public static class TowerModes
    {
        public const string PrefKey = "AdamsHaven.Mode";
        public const string LastSlotKey = "AdamsHaven.Tower.LastSlot";   // the prototype's own key (AdamsHavenPrototype)
        public const string Tower = "tower", Lair = "lair";

        public static string Active = Tower;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot() { Active = PlayerPrefs.GetString(PrefKey, Tower) == Lair ? Lair : Tower; }

        // Honoured only in Play: EditMode tests and editor tools always see the Tower.
        public static bool IsLair { get { return Application.isPlaying && Active == Lair; } }
        public static string Current { get { return IsLair ? Lair : Tower; } }
        public static string FolderName(string mode) { return mode == Lair ? "AdamsHavenDungeon" : "AdamsHavenTower"; }
        public static string Title { get { return IsLair ? "CELESTIUM DUNGEON" : "CELESTIUM TOWER"; } }
        public static string Other { get { return IsLair ? Tower : Lair; } }

        public static string CanSwitch(AdamsHavenPrototype tower)
        {
            if (tower == null || tower.Rules == null) return "The game is still loading.";
            if (tower.InBattle || tower.ExpeditionOpen) return "Finish the battle or expedition first.";
            if (tower.Rules.State.siegeBattle) return "The heroes are fighting a siege.";
            return null;
        }

        // Saves the open slot, swaps the remembered slot for the other mode's, and reloads the scene; the prototype's
        // Awake then opens that mode's folder.
        public static string SwitchTo(string mode, AdamsHavenPrototype tower)
        {
            mode = mode == Lair ? Lair : Tower;
            string blocked = CanSwitch(tower);
            if (blocked != null) return blocked;
            tower.SaveNow();
            PlayerPrefs.SetInt(LastSlotKey + "." + Active, PlayerPrefs.GetInt(LastSlotKey, 1));
            PlayerPrefs.SetInt(LastSlotKey, PlayerPrefs.GetInt(LastSlotKey + "." + mode, 1));
            PlayerPrefs.SetString(PrefKey, mode);
            PlayerPrefs.Save();
            Active = mode;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return null;
        }
    }
}
