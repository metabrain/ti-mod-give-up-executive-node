# AI trade acceptance — installed-game investigation, 2026-10-19

Read-only inspection of Assembly-CSharp SHA-256 `4a4b9aae4154e444e9727204205d2d42ae8ed9e1c5f92cdc1280074a259d8350`, using ILSpyCmd 9.1.0.7988. No trade was executed and no gameplay code was changed. These are native assembly rules, not a claim that every runtime mod patch was inspected.

## Evaluation path

`DiplomacyController.EvaluateTrade` constructs both sides' offers and calls `TradeAI.IsAgreementAcceptable`, passing the AI as assessing faction. The result enables or disables the execution button. Randomness chooses the wording variant, not the acceptance decision. An unchanged AI-originated offer (or the native AI-trade test mode) has an explicit UI override enabling acceptance; an empty table is still disabled afterward.

## Scores and acceptance

Each faction independently values the agreement, including the human player's side using the game's valuation model. `GetCategoryScore` groups resources, orgs, projects, habs and diplomacy. Outgoing resources have their value multiplied by distrust; outgoing orgs use distrust times 1.07, habs distrust times 1.09, and projects distrust minus 0.5. Incoming items are valued for the receiving faction.

`ScoreAgreement` sums positive category totals and adds three times the negative category totals. This is a penalty on category-level net losses, not a blanket multiplier on every individual item.

Let A be the AI's resulting score and B the other side's score. Favorability is zero when A is nonpositive, one when A is positive and B is nonpositive, and otherwise A/(A+B). After hard gates pass, acceptance requires favorability at least:

`0.30 + 0.19 * clamp(distrust - 1.20, 0, 1)`

Thus the threshold ranges from 30% to 49% of the calculated combined gain. This is not a 30–49% acceptance probability or a fraction of raw goods exchanged.

## Distrust

Permanent allies use 1.025. Otherwise start with `(1 + 0.1 * ideologicalDistance) * (1 + warProgress/2)`. If the judging faction is the strongest human faction, halve the excess over one. Otherwise, if its counterpart is strongest, multiply by 1.2. The human player's modeled score uses the AI's war progress toward that player rather than the player's hate toward the AI.

`FactionsGoToWarProgress` is clamped to 0–1, usually hate divided by the faction's war threshold (subject to sufficient intel for human factions). Truces return zero; special alien/late-victory situations have explicit overrides.

## Hard refusal gates

From the assessing AI's perspective: if receiving no habs, it will not give more than one; otherwise it will not give more habs than it receives. It will not give more than two additional orgs over the number received.

For each tradable resource and mission control, calculate the net change in monthly income from traded orgs and habs. A negative MC change that makes the MC balance negative is rejected. For other negative income changes, reject if existing monthly income is nonpositive, new income is nonpositive, or new income is below 60% of the old income. Extra upfront resources do not alter this income gate.

Separate eligibility methods constrain what can be put on the table, including treaty willingness and hab restrictions. `WillingToTrade` also rejects an ideology-x distance of at least three unless the judging faction is malleable.

## Valuation and feedback

Resource value depends on relative resource valuation, faction AI priorities, stocks and estimated days of supply using income/spending history. Quantities are evaluated in segments as hypothetical stocks change: there is no fixed universal cash exchange rate. Org evaluation includes income, stats, missions and projects; project evaluation includes research cost, category/role priorities, critical-tech status and research availability. Habs use their economic/strategic evaluation and location.

Fair versus generous acceptance additionally checks a meaningful-score threshold and the excess favorability over the minimum. A zero favorability (also used by early hard failures) receives the strongest rejection text; a positive score below threshold receives the milder rejection. Thus the text alone does not identify a hard blocker.

Possible future readout: both scores, distrust, favorability/threshold, and explicit hard-refusal reasons. No such feature was implemented in this investigation.

## Implemented in 0.3.15

Delayed Harmony postfixes on DiplomacyController.EvaluateTrade and Setup read the actual offers and the native button result. Five native category scores per faction are obtained through cached reflection, then combined with the native negative-category multiplier and favorability edge cases. No trade planner or native evaluation is invoked by this module. Hard gates mirror the inspected validator; pure boundary tests cover them. An untouched AI offer override is explicitly identified.

Serialized ui bundle inspection: Councilor 315x300, feedback bottom 70, AI table scroll 315x310 immediately below. The new 112px panel sits below Councilor; table height decreases by120 with bottom retained. Original list geometry is restored on cleanup. Tooltip style is copied from a native trigger, with fresh delegates and no copied callbacks. A single startup scan supports already-open screens; later refreshes are event-driven. No campaign data is retained or serialized.

Validation: installed Assembly-CSharp SHA256 4a4b9aae4154e444e9727204205d2d42ae8ed9e1c5f92cdc1280074a259d8350; SDK8.0.426, ILSpy9.1.0.7988, UnityPy1.25.0. Live layout/hover, resource and org/hab offers, AI offers, reopening, campaign reload and UMM toggling remain runtime checks.

## 0.3.16 refinement
Removed favorability percentage from the UI. The result shows the first hard failure and total count; hovering benefits or result enumerates all violations (including simultaneous hab, org and multiple resource failures), their values/limits, and the complete rule list. Internal favorability still distinguishes valuation rejection from hard restrictions. No native early return is used in diagnostic enumeration.

## 0.3.17 refinement
Removed duplicate accepted/rejected/threshold status. The extra text now only identifies hard restrictions or their AI-offer bypass. Transparent owned raycast surfaces cover the entire breakdown and native leader dialogue; both show the same tooltip. Cleanup destroys both surfaces without modifying native dialogue components.

## 0.3.18 refinement
All violated restrictions appear inline as a numbered list. The restrictions viewport grows from72 to140px and supports vertical scrolling for longer lists, with fixed readable15px text. The native offer list adjusts from its saved geometry, keeping its bottom fixed, and restores on cleanup. AI-offer bypasses retain the complete list.

## 0.3.19 refinement
Restrictions viewport now sizes from0 to140px. Empty restriction text disables the viewport and reserves only48px total for the two benefit lines and padding, restoring72px of offer-list space compared with the original fixed120px reservation.

## Treaty diagnostics (0.3.21)

Installed methods traced: DiplomacyController.LoadBankValues, TIFactionState.CanTradeTreaty/CanTradeNAP/CanTradeTruce/CanTradeIntelSharing, FactionGoal_NonAggressionPact.ValidNewGoal/GoalFulfilled, and AIEvaluators.GetWillingnessToTradeNAP/GetWillingnessToTradeTruce/GetWillingnessToShareIntel. Native eligibility and willingness decide the heading; read-only checks explain the constituent conditions. TreatyNeeds isolates the ordinary strategic-need branches and is covered by13 additional boundary/branch checks. Native alien alignment branches are preserved literally even where surprising. Intel permanent allies skip security and ideological-neighbor branches, but the native strategic-needs assignment still runs; diagnostics reflect that behavior.

Native BodyText delegates are wrapped and preserved, refreshed after bank loading/setup, and restored only if still owned. Hidden informational rows are exposed for unavailable treaties, without enabling treaty actions. Both banks, treaty table entries and intelligence-exchange entries are covered. Exceptions fall back to the original tooltip. No native DLL, save state or trade acceptance code is altered. These methods read game caches and values only; jealousy is queried with processPeriodicChange=false. Tooltip evaluations occur on demand. Full native lifecycle and visual tests remain pending.
