# Player UI navigation audit after the 10:19 Handbook failure

Status: **static review complete; one confirmed stale probe precondition, already fixed by root; no second confirmed navigation blocker found.** This is not a Player execution pass.

## Inputs and ownership

- Repository HEAD at review: `0f04e51a2add69ca69f44c2d0c7b8425367e7a89`.
- Reviewed the current on-disk `PlayerVerification.cs` (ProbeRevision `p07-player-art-ui-18`) and `PlayerVerification.V06.cs`, plus their actual GameApp entrypoints and uGUI pooling helpers. The first file already contains root's uncommitted Handbook precondition fix. `bindings.json` records exact bytes read; these must not be represented as pristine HEAD bytes.
- Review only: no Unity/Player operation, production edit, preference mutation, index operation, or criterion change. Root owns integration and subsequent actual Player validation.
- Actual 10:19 failure supplied by root: NormalReplay step 2, `Handbook scatter row missing`. Native display-only mode 0 failure remains separate and unchanged.

## Confirmed cause and accepted minimal correction

**PVNAV01 — Handbook tab is persistent presentation state.**

`VerifyV07MenuAndSettings` in `PlayerVerification.V06.cs:250–255` opens the Handbook, clicks `HandbookColors`, and closes it. `GameApp.V05.cs:13,22,27–28` retains `handbookColors`; closing the modal does not reset it. The subsequent `VerifyHandbookPureReads` originally opened the Handbook and immediately assumed Hand rows were visible. The product correctly remembered the Color page; the probe had not established its required page.

Root's current `PlayerVerification.cs:556` now performs the actual `PressTargetReady("HandbookHands")` before the unchanged scatter/all-hand assertions. The existing subsequent Colors check, explicit switch back to Hands, twenty repeated opens, state/pending/save equality, and real controls remain intact. This is the correct narrow repair; resetting the product's remembered tab is unnecessary. Root reports two affected real Handbook tests passing; this review does not independently claim to have run them.

## Later routes checked against current production

| Probe route | Actual implementation evidence and result |
|---|---|
| Three-resolution score details | `PlayerVerification.cs:1258–1295` opens `ShowBreakdown`, presses `FullScoreDetails`, then locates the active `ScoreDetail` ScrollRect. `GameApp.Art07ScoreDrawer.cs:13,73` and `GameApp.Modals.cs:305,317` implement exactly this two-step path. No remaining Factor0/one-step full-detail assumption in these two probe files. |
| Lock → submit and Debug24 replay | Main Run waits `WaitReady` around commands; `Debug24Action` waits readiness, optionally skips actual presentation, then waits again. `ReplayV06DebugTrace` similarly waits `WaitIdle`. The new lock-visual Busy is not bypassed by an immediate keyboard submit sequence. The keyboard adapter here is used for Escape cancellation, not lock/submit. |
| Dice sheet | `VerifyV07DiceSheet` uses `ShowDie`, active `Face_Fxx`, `CurrentFace`, `SelectedH0`, `LayersBody`, `ProbabilityGroups`, probability values/bars, and `RotatePreview`. `DrawDiceSheet07`/face-net callbacks in `GameApp.Modals.cs:147–239` provide those controls. The probe explicitly chooses the expanded physical-face probability view if necessary, so its persistent boolean is handled. |
| Debug entry and navigation | `VerifyV06UiAndIsolation` opens Pause → Settings → SettingsTab2, tests the exact password, then clicks `CopyCurrentToDebug`. Current `GameApp.Art07Settings.cs` calls `ShowDebugEntry07`. `GameApp.Debug.cs:107–123` maps current sidebar names to pages; `DebugTab0/1/2/3` used by the probe exist. In particular `DebugTab3` is the items/services navigation entry backed by the same resource form, and the Wallet/budget fields remain present in that form. No assumption that the old sidebar order equals page index is used. |
| Debug draft edits and return | Add/owned/enchantment controls, `FreshHand`, `AddDie`, batch face controls, `DebugLock`, `MaxLevels`, resource fields, item controls, `ApplyDebugScene`, `RunDebugScene`, `ResumeDebugSave`, and `ReturnNormal` resolve to the current implementations in `GameApp.Debug.cs`. Actual Apply remains one command; the probe waits, verifies the committed scene, and returns through the production normal-workspace path. |
| 32-die remote selection | `VerifyV06Targets32` opens Item → Use → RemoteTargets. Current `GameApp.Modals.cs:380–439` creates a fresh chosen list and `targetScrollPosition=1` per opening, `RemoteTargetList`, `RemoteDieD031/D032`, `RemotePip...`, and disabled-until-two-targets `RemoteConfirm`. Thus the preceding bottom-scroll capture does not persist its scroll or selections into the subsequent wheel test. Cancel and commit state/save checks remain. |
| 32-die upgrade/delete targets | `OpenShopOffer07` selects the category's actual bottom tab, then `Inspect` scoped under `Product_<offerId>`. `GameApp.Shop.cs:133–180` dispatches upgrades/deletion to `TargetDie(..., scrollPosition=1)`. `DrawDiceSheet07` exposes `ShopTargetList`, `Target_D032`, `Face_F01`, and `ConfirmPurchase`; target callbacks preserve scroll only within that specific open dialog. Cancel/reopen does not reuse a stale implicit target. |
| Shop tabs and refresh | Current fixed `Offer_<id>` shelf and lower `Product_<id>` names match the tab test. Refresh text in `GameApp.Art07Shop.cs:89` is exactly the probe's `本店首次免费` or `刷新控制器  <price>`. Reopen explicitly selects Tab0; O07 explicitly selects Tab3 before inspecting. Capacity text is read from the current `ControllerCapacity07` node. |
| Controller drag and broken mimic chain | The probe targets `OrderCard<id>` and inserts at the destination card's right edge (`rect.xMax-8`), matching `GameApp.Controllers.cs` half-card insertion semantics. It does not rely on an old die-center swap gesture. Cancel/outside/Escape/focus checks are retained. The broken-chain strings remain produced by the current controller-state renderer. Fresh order lists start at the top before the offscreen terminal-card wheel test. |
| Pooling and active lookups | `ActiveControl` filters active hierarchy and requires exactly one named control. `PressTargetReady` re-finds after render registration and requires the actual first raycast Button to be the target. `CaptureResolutionSet` likewise filters active ScrollRects. `UiKit.Scroll:112–118` resets leased scroll state to the top; inactive pooled modal/card objects are not mistaken for the live controls. |
| Stress and roll presentation | Stress captures use the same repaired two-step detail helper. Stress Reorder uses a formal command; its timing keeps the existing 140 ms reorder before the next pending-selection operation. No full-hierarchy transaction-clock lookup, old `AllControllerStates` text assertion, or old Factor control assertion exists in these two probe files. `VerifyFirstAnimatedRoll`/`VerifyLandedDice` retain their actual RollPivot/face-orientation checks. |
| Display UI and process recovery | Existing Mode0/1/2, ApplyDisplay, ConfirmDisplay names match `GameApp.Display.cs`. The native mode/confirmation, timeout, actual-size, fresh-process recovery, performance duration and coverage requirements were not modified. The real mode 0 failure is not converted into a navigation success. |

## Result and remaining execution boundary

No additional source-proven wrong entrypoint, renamed control, persisted-tab precondition, pool-selection ambiguity, or obsolete drag/Busy assumption was found after PVNAV01 in the requested two verification files. No additional production or probe patch is proposed by this review.

The rest of the Player workflow still needs to execute in the actual newly built Player. Static agreement cannot establish native display support, GPU text visibility, runtime raycasts, input timing, audio routing, performance, or complete delivery acceptance. Preserve the 10:19 failure and native mode 0 failure as failed evidence, and bind the later Player result to its new probe/source identity.
