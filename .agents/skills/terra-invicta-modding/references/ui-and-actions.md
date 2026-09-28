# UI and game-state actions

## Runtime UI lifecycle

Find the controller's initialization and refresh methods in the installed assembly. A narrow Harmony postfix is often sufficient; use the mod ID as the Harmony owner and unpatch only that owner. Support already-initialized scene controllers when enabling mid-campaign. Avoid editing the game DLL or rebuilding the entire UI bundle to add one control.

Clone an existing compatible visual under the correct parent. Preserve the original controller's reference to the original button. Map source components to cloned counterparts using hierarchy paths/indices; avoid translated label text. Check assumptions before modifying layout and retain original rect properties for cleanup.

Replace copied click events completely:

```csharp
cloneButton.onClick = new Button.ButtonClickedEvent();
cloneButton.onClick.AddListener(HandleNewAction);
```

`RemoveAllListeners()` is not a substitute for removing serialized persistent callbacks. Inspect other copied components, events, controller pointers, and tutorial markers too. Ensure a copied tutorial target does not register as the original action.

Keep confirmation dialogs inactive while wiring them. Inactive parents are skipped by the default parent lookup. Use and validate:

```csharp
var confirmButton = confirmText.GetComponentInParent<Button>(true);
if (confirmButton == null)
    throw new InvalidOperationException("Confirmation button is missing.");
```

Bind both confirm and cancel to new handlers; never leave the original destructive action attached. Use contextual errors and clean up a partial initialization so an exception cannot leave broken controls behind.

Choose layout from evidence: sibling ordering for a LayoutGroup; explicit RectTransform adjustments for a manually positioned footer. Account for neighboring controls, scroll clipping, minimum label size, localization, and UI scale. A cloned rect initially overlaps its source. Do not assume empty space from a controller's sizing argument.

Unpatching does not remove clones or restore geometry. On disable/unload/controller destruction, cancel pending confirmations, remove owned listeners/objects, restore only owned layout changes, and clear references. Guard against duplicate insertion and destroyed Unity objects; test disable/re-enable and a second campaign.

## Confirmation and state mutation

The displayed disabled state is not authorization to mutate state. Validate at opening, confirmation, and execution as relevant:

1. Resolve the current player and target; check exact eligibility.
2. Capture the specific target, player, and affected entity in transient confirmation state.
3. Cancel on selection changes, target loss/replacement, closing the panel, switching relevant tabs, or mod disable.
4. Consume confirmation before dispatch to prevent double clicks/reentrancy.
5. Recheck eligibility and entity identity immediately before mutation.

Use the game's native operation when it maintains owner lists, events, caches, priorities, defense, armies, diplomacy, or history. A direct field assignment may appear to work while leaving those systems inconsistent. Inspect the game's player-action dispatch before introducing a custom action; do not assume synchronous execution, serialization, or multiplayer semantics across builds.

Choose an existing cause/reason value based on actual branches and consumers. Do not invent enum values or repurpose a coup/trade merely to get an ownership change. Record remaining uncertainty for runtime testing.

Keep transient UI state out of campaign saves. If persistent state is necessary, inspect registration, serialization, migration, and mod-removal behavior explicitly. Disabling a mod does not undo legitimate state changes already saved.

## Verification and debugging

Test pure eligibility/confirmation/action behavior offline where possible, including cancel, stale selection, ownership loss, entity replacement, nulls, double execution, and changes between dispatch and execution. Linking production logic into tests with game stand-ins is useful, but does not test Unity lifecycle or the real game's mutation method.

For a loaded mod with missing UI:

- Read UMM's log first and filter the mod ID; “Active” does not mean UI initialization succeeded.
- Distinguish unrelated mods' errors from the first failure in this mod.
- Match exception offsets against the installed DLL, then fix the actual failure before changing positioning.
- Record the patch/build version. A changed DLL on disk does not replace the loaded assembly; restart for verification.

Runtime acceptance should cover visible layout, eligibility, actual confirm/cancel callbacks, original neighboring controls, side effects, duplicate/reopened screens, save/load, and mod removal. State whether evidence came from direct observation or the user, and do not expand “it works” into proof of every acceptance case.
