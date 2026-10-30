from pathlib import Path
import json
import re
import sys

base=Path(__file__).resolve().parent
report=json.loads(Path(sys.argv[1]).read_text(encoding='utf-8'))
tests=report['results']
gold=json.loads((base.parents[2]/'DicePrototype/Assets/Game/Data/GoldenScores.json').read_text(encoding='utf-8'))
mapping={}
def add(key,*needles,note=''):
    matches=[x for x in tests if any(n in x['name'] for n in needles)]
    if not matches: raise ValueError('No real tests for '+key+': '+str(needles))
    previous=mapping.setdefault(key,{'id':key,'tests':[],'note':note})
    previous['tests']=list({x['name']:x for x in previous['tests']+matches}.values())
    if note:previous['note']=note

for i in range(1,41):
    cid=f'C{i:02}'
    add(cid,f'CONTENT_ControllerPurchaseDuplicateAndSell({cid})')
    selected=[x['id'] for x in gold if cid in x['controllerIds']]
    add(cid,*([f'CONTENT-{cid}-',f'V05-TRIGGER-{cid}-']+[f'GOLD-{s}' for s in selected])) if selected or any(f'V05-TRIGGER-{cid}-' in x['name'] for x in tests) else None
for cid in ['C04','C16','C21','C22','C23']:
    add(cid,'BudgetsAndCapacityAreIndependentOfDiceAndCombineAdditively','V05_BudgetsAndCapacityUseIndependentControllerRules')
add('C01','FLOW_E01_RewardUsesOldBalance','V05_InterestCapsPrincipalNotBalance','V05_QualifiedDoesNotPayOrCreateShopAndReadQuotesArePure')
add('C05','FLOW_A09_A11_C05OnlyAfterRollOnceAndSurvivesLoad','FLOW_A10_LockingAfterLastRollDoesNotGrantC05')
add('C07','FLOW_C01_C02_CopyUsesFrozenScoreAndDeepCopies','FLOW_C03_C04_CopyDoesNotTriggerLaterOrOnEffectivePair','V05_CopyThenGrowthUsesCopiedTargetFaceValueAndSourceIsIndependent')
add('C15','FLOW_C05_C06_DiffusionUsesPhysicalFaceAfterScoring')
add('C26','V05_C26ThenC38LeavesFiveRedOneBlueAndScoreFrozen')
add('C29','V05_C29ReenablesFourSlotStraightStudyWithoutColorHands','HandbookC29ChangesOnlyStraightThresholdAndNextReadRefreshes')
for cid in ['C34','C35']:
    add(cid,'GroupTransferFixedVectors','MissingRepeatedAndExhaustedDonorDigitsMatchReferenceVectors','Reviewer_512ExtremeWeightPermutedProbabilitiesMatchIndependentFractions','V05_ProbabilityControllersUseSameFinalVectorAsActualRoll')
for cid in ['C36','C37']:
    add(cid,'V05_GrowthShrinkCancelBeforeSingleClampAtBothBoundaries','V05_RemoteGrowthChangesFrozenPhysicalFaceAndNeverOverridePip')
add('C38','V05_C26ThenC38LeavesFiveRedOneBlueAndScoreFrozen','V05_CopyThenGrowthUsesCopiedTargetFaceValueAndSourceIsIndependent')
effects={
1:['V05_ControllerOfferPersistenceAndOriginalInstanceSurviveReadBuyAndReload'],
2:['V05_ControllerOfferPersistenceAndOriginalInstanceSurviveReadBuyAndReload'],
3:['V05_SlotlessFitsBeyondFourAndReplaceUsesFinalEquipment'],
4:['V05_ReplacingAtTenDoesNotCreateEleventhInstanceAndNewEleventhRejects'],
5:['Reviewer_E34TenItemsReplacingOrdinaryWithSlotlessFreesOnlyItsSlot'],
6:['V05_SlotlessFitsBeyondFourAndReplaceUsesFinalEquipment'],
7:['BaseEnchantIsOncePerCardAndPairAddIsNotPerDieOrMultipliedByGold'],
8:['V05-TRIGGER-C10-miss'],
9:['MultipleMultiplierEnchantmentsUseOneFinalFloor'],
10:['BoundaryTriggersReadFrozenOriginalAndEffectiveLayers','Reviewer_1024GeneratedScoresMatchAll26ReferenceFieldsAndArePure'],
11:['GOLD-S072'],12:['GOLD-S070'],13:['GOLD-S074','V05_GrowthShrinkCancelBeforeSingleClampAtBothBoundaries'],
14:['RawEnchantmentBoundariesAndNonSubmitFallbackConsumeExactlyOneDraw'],
15:['PreviewDoesNotAdvanceAnyRngOrApplyCopyOrDye','V05_C26ThenC38LeavesFiveRedOneBlueAndScoreFrozen'],
16:['FLOW_E13_CopySaveFailureDoesNotApplyUntilCommitOrRepeatOnRetry','DuplicateReceiptPrecedesSequenceAndBusyChecks'],
17:['V05_CopyThenGrowthUsesCopiedTargetFaceValueAndSourceIsIndependent'],18:['V05_C26ThenC38LeavesFiveRedOneBlueAndScoreFrozen'],
19:['V05_GrowthShrinkCancelBeforeSingleClampAtBothBoundaries'],20:['V05_CopyThenGrowthUsesCopiedTargetFaceValueAndSourceIsIndependent'],
21:['GOLD-S100'],22:['GOLD-S025'],23:['V05_QualifiedDoesNotPayOrCreateShopAndReadQuotesArePure'],
24:['V05_ContinueAfterQualifiedKeepsOrdinalAndPaysOnlyRemainingAtEnd'],25:['V05_FinalSubmissionAutoEndsAlreadyQualifiedStage'],
26:['LastSubmissionTargetWinsBeforeExhaustionAndNewHandClearsEffects','V05_EndStageRejectsUnqualifiedPendingBusyAndSavePending'],
27:['V05_ContinueAfterQualifiedKeepsOrdinalAndPaysOnlyRemainingAtEnd','V05_EndStageWithLockedHandDoesNotSubmitOrRevertUsedActions'],
28:['V05_QualifiedI06AddsRewardAndRestoreDoesNotPay'],
29:['V05_EndStageWithLockedHandDoesNotSubmitOrRevertUsedActions','V05_EndStageRejectsUnqualifiedPendingBusyAndSavePending'],
30:['V05_AllGlassLostAfterQualificationUsesSingleSettlementAndFinalPriority'],
31:['TwentyFourStageFlowSupportsEightRiskBranchesAndNoFinalShop','V05_AllGlassLostAfterQualificationUsesSingleSettlementAndFinalPriority(True)'],
32:['HandbookAlwaysContainsThirteenRowsAndSeparatesTemporaryFormationFromUpgradeEligibility','HandbookC29ChangesOnlyStraightThresholdAndNextReadRefreshes'],
33:['V05_SlotlessFitsBeyondFourAndReplaceUsesFinalEquipment'],34:['Reviewer_E34TenItemsReplacingOrdinaryWithSlotlessFreesOnlyItsSlot'],
35:['Reviewer_E35SameOrEmptyEnchantCannotReplaceAndDoesNotCharge','ReplacementPoolsExcludeNoneAndCurrentAndOneChoiceStillAdvancesRng'],
36:['Reviewer_ExactNetReplacementBoundaryE36E37(13,True)'],37:['Reviewer_ExactNetReplacementBoundaryE36E37(12,False)'],
38:['V05_ReplacementInsufficientMoneyAndWriteFailureAreAtomic'],39:['V05_CopyThenGrowthUsesCopiedTargetFaceValueAndSourceIsIndependent'],
40:['V05_ContinueAfterQualifiedKeepsOrdinalAndPaysOnlyRemainingAtEnd']}
for i,terms in effects.items():
    add(f'E{i:02}',*terms,note='Rule read-model covered; actual modal clicks/input blocking/RNG twenty-open test requires Unity PlayMode/Player evidence.' if i==32 else '')
for i in range(1,8):
    iid=f'I{i:02}'
    add(iid,f'CONTENT_ItemSuccessfulUseAndDuplicateReceiptDoNotConsumeAgain({iid})',f'CONTENT_ItemRejectsOutsideWindowWithoutConsumption({iid})')
add('I01','CONTENT_HandBuffDoesNotStack(I01)','FLOW_C05_C06_DiffusionUsesPhysicalFaceAfterScoring')
add('I02','FLOW_A07_TemporaryDiceDoNotExpandCapacity','FLOW_C12_TemporaryLimitIsSimultaneousNotHistorical')
add('I03','FLOW_C07_RemoteAllowsAbsentPipAndPreservesPhysicalFace','FLOW_C08_RemoteRejectsBadTargetsAtomically','FLOW_C09_C11_BrokenRemoteTargetsConsumeItemAndNoDiceFails')
for iid in ['I04','I05']:add(iid,'FLOW_D05_D06_ProbabilityBuffsUsePermanentPipsAndDoNotStack',f'V05_BiasItemRequiresFutureLegalRollOnMatchingUnlockedDie({iid})')
add('I06','V05_QualifiedI06AddsRewardAndRestoreDoesNotPay')
add('I07','FLOW_A12_ExtraRollAndSubmissionPersistForStage')
add('O01','ServicesO01O02O04AndNewDieCapacityRespectLimits','CONTENT_ServiceDuplicateReceiptDoesNotRepeatMutation(O01)','FLOW_E08_MaxedServiceRejects(O01)')
add('O02','FLOW_E05_E06_DiscountChargesOldPriceAndMinimumTwo','FLOW_E08_MaxedServiceRejects(O02)','CONTENT_ServiceDuplicateReceiptDoesNotRepeatMutation(O02)')
add('O03','FLOW_E11_NewDiePreviewFixedAndPurchaseClones','CONTENT_O03_FullPermanentPoolRejectsAndExistingCandidatesNeverRefresh','SixthDieRequiresIndependentCapacityBeforeSixKindStudy')
add('O04','FLOW_E12_LastPermanentDieCannotBeDeletedEvenWithTemporaryDie','CONTENT_ServiceDuplicateReceiptDoesNotRepeatMutation(O04)','ServicesO01O02O04AndNewDieCapacityRespectLimits')
for i in range(1,8):
    uid=f'U{i:02}'
    add(uid,f'CONTENT_UpgradeAppliesOnlyToTargetAndIsIdempotent({uid})',f'CONTENT_UpgradeRejectsMissingTargetWithoutConsumingOffer({uid})')
    if i<7:add(uid,f'FLOW_E09_IdenticalUpgradeRejectsWithoutMoneyOrRandomness({uid})')
add('U01','Probability_All2916TextureAndBuffCombinations','FLOW_D07_TextureReplacementPreservesOtherAttributes')
add('U02','Probability_All2916TextureAndBuffCombinations','FLOW_D07_TextureReplacementPreservesOtherAttributes')
add('U05','FLOW_C10_LockedGlassNeverRollsOrBreaks','V05_AllGlassLostAfterQualificationUsesSingleSettlementAndFinalPriority')
add('U07','FLOW_E10_TransformExcludesOldPipAndPreservesAttributes','FLOW_E13_TransformSaveFailureRetainsExactCandidateAndBlocksNewActions')
assert len(mapping)==98
for item in mapping.values():item['managedStatus']='FAIL' if any(t['result']!='Passed' for t in item['tests']) else 'PASS'
out={'scope':'Appendix C requirement-to-actual-managed-check traceability, not Unity/Player acceptance and not a claim that every subcase is exhaustive',
     'report':str(Path(sys.argv[1]).as_posix()),'uniqueRequirementIds':98,'mappedRequirementIds':len(mapping),'rows':list(mapping.values())}
(base/'appendix-c-coverage.json').write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf-8')
lines=['# 附录C 98项实际托管检查映射','','这是规则/会话托管检查的追溯索引，不代替Unity、平台构建、视觉和交互验收。测试名称来自实际报告，未用静态关键词出现充当执行。一个用例可以覆盖多个条件；映射不宣称穷尽所有输入。','','| ID | 托管结果 | 已执行检查 | 补充范围 |','| --- | --- | --- | --- |']
for row in out['rows']:
    lines.append('| '+row['id']+' | '+row['managedStatus']+' | '+'<br>'.join(t['name'] for t in row['tests'])+' | '+row['note']+' |')
(base/'APPENDIX_C_COVERAGE.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print('Mapped',len(mapping),'requirements;',sum(r['managedStatus']=='PASS' for r in mapping.values()),'managed pass;',sum(r['managedStatus']=='FAIL' for r in mapping.values()),'managed fail')
