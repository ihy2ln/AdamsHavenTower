# BM fix: NullReferenceException in BattleMode.OnGUI after leaving a battle

Paste this into the Battle Mode (BM) session.

---

BM bugfix. `BattleMode.OnGUI` throws a NullReferenceException every time a battle is left from its result screen:

```
NullReferenceException: Object reference not set to an instance of an object
BattleMode.OnGUI () (at Assets/Scripts/Battle/BattleMode.cs:908)
```

It was logged three times in one Tower play session (Unity 6000.6.3f1, Editor, Play mode), once per finished battle. The Tower session did not touch any Battle files.

## Root cause

In `OnGUI` (around lines 900-911), the result screen is drawn and then `battle` is read again on the same pass:

```csharp
if (battle.Finished && !Busy)
{
    ...
    DrawResult();                         // its return button calls ExitBattle()
}
if (paint) DrawDiscarding();
if (battle.EpiphanyCard != null && !Busy && !battle.Finished) DrawEpiphany();   // line 908: battle is null here
if (showLog) DrawLog();                   // DrawLog also reads battle.Log
```

`DrawResult` (around line 2065) calls `ExitBattle()` when the player taps the return button (`ReturnLabel`, e.g. "BACK TO THE TOWER"). `ExitBattle` (around line 522) sets `battle = null`, releases the stage and invokes the leave callback, which can destroy this component. Control then falls back into `OnGUI`, and line 908 dereferences `battle`. Any other `battle.` access later in that `OnGUI` pass, and any other `OnGUI` call before the component is destroyed, can throw the same way.

## Fix wanted

1. In `OnGUI`, return straight after anything that can end the battle. For example, after `DrawResult();`:
   `if (battle == null) { GUI.matrix = old; return; }`
   Also guard the top of `OnGUI`: `if (battle == null) return;`. Match how the method already restores `GUI.matrix`.
2. Check every other caller of `ExitBattle()` (withdraw confirm, any leave buttons) for the same pattern. Any code after the call that touches `battle` in the same frame needs the same early return.
3. Optionally, defer the teardown: set a `pendingExit` flag in the button handler and call `ExitBattle()` from `Update`/`LateUpdate`. Then `OnGUI` never sees a half-torn-down battle.

## Verify

- Win a dock BATTLE from the Tower, press the return button, and confirm no exception in the Console.
- Repeat with a loss/withdraw, and with a siege DEFEND IN BATTLE. The Tower side calls `rules.ResolveSiegeBattle` from the leave callback.
- Run the `^Battle` EditMode tests.

Commit with the usual BM label, and keep to Battle files only (the Tower session owns `Tower*.cs` / `Lair*.cs`).
