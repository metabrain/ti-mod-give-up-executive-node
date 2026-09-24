# Give up Nation — 0.1.1

Adds **Give up Nation** beside **Abandon Nation** in the nation's Policies tab. The existing Auto-Renew Abandon checkbox remains on the row below.

The button is enabled when your faction owns that nation's executive control point. Pressing it opens a final confirmation. Confirming makes only the executive point unowned; your other points remain yours. This is an ownership loss, not a temporary crackdown. Regaining the executive requires normal gameplay.

Cancel makes no changes. Changing the selected nation, losing ownership, leaving the Policies tab, or closing the nation panel invalidates an open confirmation. Alien nations are excluded because the game's ownership-change operation does not permit their points to become unowned.

This mod adds no campaign save fields. Ownership changes use native game state and will be saved normally. Disabling or removing the mod removes its UI but does not undo an already confirmed surrender. Existing/new campaigns are intended to work; save/load and runtime behavior have not yet been tested.

Requires Terra Invicta and Unity Mod Manager 0.32.4 or later, with the Terra Invicta profile using `Mods/Enabled` and `ModInfo.json`. Compiled against the locally inspected installation; compatibility with other game builds is unverified.

**Build status:** compiled with 13 passing offline confirmation/action checks. After the 0.1.1 popup-initialization fix, the user reported that the UI works in-game. Executive-release side effects and save/load still need verification. The game installation remains read-only except for explicitly authorized installation/update of this mod; the build tool never deploys to it.

Created using https://github.com/Laurentiu-Andronache/ti-mod-template
