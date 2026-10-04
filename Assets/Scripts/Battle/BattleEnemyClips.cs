using UnityEngine;

// Monsters drawn from their own anime clips (CM 10.3.5): Tools/build_enemy_clip_specs.py + produce_fighter_clips.py
// write Resources/AdamsHaven/BattleClips/<form id>/ with an idle loop, one action per move (2 to 5 by tier), a hit
// reaction, a block, a knockdown and a victory pose, all facing the left. A monster with a complete set plays them
// like a fighter (BattleClips.cs: the card's action timed to its contact frame, reactions, the victory after a loss);
// one without keeps its painted cutout. Monsters have no combos.
public sealed partial class BattleMode
{
    void AddEnemyClipRig(BattleUnit unit)
    {
        if (unit == null || !unit.Enemy || fieldRigs.ContainsKey(unit) || !PreferClips) return;
        if (FieldPicture(unit) != null || string.IsNullOrEmpty(unit.Species)) return;
        var rig = CreateClipRig(unit, unit.Species);
        if (rig != null) fieldRigs.Add(unit, rig);
    }
}
