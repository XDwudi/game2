# PlayMode schema3 fixture repair

Input: P0 v0.5 / config p0.5.0 / save schema 3. Production code was read-only. Scope: six PlayMode test source files and this evidence directory. Root exclusively owns Unity execution.

## Observed failures

`evidence/v05/unity/playmode-01.json` records 24 tests, 14 passed, 10 failed. Nine failures occurred while loading explicit boundary fixtures: manually created controller instances still used the old `TEST_*`/`OFFER_*` identity and default acquisition serial; manually created controller offers also carried a zero base price. Schema 3 correctly rejects these before the interaction under test.

The tutorial test retained a v0.4 score (62) and automatic stage-end expectation. Its unchanged fixed dice are 4 red, 4 red, 2 blue, 3 blue, 6 blue. Under the authorized v0.5 red curve its score is `(19 + 2 * 3) * 2 * 1.5 = 75`. With submissions remaining, the correct phase after this qualifying submit is HandReady until the explicit EndStage command.

## Changes

- `BoundaryController` allocates a fresh acquisition serial from `nextItemSerial`, encodes the `ITEM_000xx` identity and preserves the requested definition/enchantment. All hand-made controller fixtures now use it, including owned cards and offers, so independent instances cannot share an identity.
- `AddBoundaryOffer` obtains the definition base price for each category and builds its fixed controller instance at fixture construction. Tests continue to exercise actual purchase pricing, eligibility and persistence.
- Tutorial asserts each factor (19 pips, 6 unused-roll bonus, 2 hand multiplier, 1.5 red multiplier) and final 75, then explicitly verifies qualified HandReady, legal EndStage, and StageSettlement before the existing real-purchase/save-isolation checks.
- No production validator, economic parameter, fixture scoring target, input route, save-failure condition, assertion coverage or test count was relaxed. Existing debug fixtures remain explicitly marked; no debug bypass was added.

## Verification

Compile-only check against installed Unity 6000.6.7f1 and package APIs: succeeded, 0 errors / 3 existing CS0649 warnings for JsonUtility-deserialized test DTO fields. Log: `compile-01.log`. Outputs are isolated below this directory and ignored.

The compile check does not establish PlayMode correctness. The root agent must run all 24 tests in the actual Unity Editor and preserve the prior failures. Source binding: `source-binding.json`.
