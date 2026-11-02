"""Offline public-title mapping; does not publish or claim runtime acceptance."""
import copy
import unittest
import publish_v07_release as publishing


class ReleaseTitleTests(unittest.TestCase):
    def test_public_titles_preserve_each_validated_scope_without_mutation(self):
        cases = [
            ({"reviewCandidate":False}, "Windows / macOS 测试版"),
            ({"reviewCandidate":True}, "原生显示待解决的限制版候选"),
            ({"reviewCandidate":True,"allowFailedWindowsPerformance":True,"windowsReference":{"windowsNativeDisplayStatus":"PENDING_P22_NOT_PASSED"}},
             "性能失败及原生显示待解决的诊断候选"),
            ({"reviewCandidate":True,"allowFailedWindowsPerformance":True,"windowsReference":{"windowsNativeDisplayStatus":"AUTOMATED_CHECKS_PASSED_PHYSICAL_EVENTS_UNTESTED"}},
             "性能失败的诊断候选（自动显示检查通过，物理场景待验）"),
        ]
        for manifest,label in cases:
            with self.subTest(label=label):
                before=copy.deepcopy(manifest)
                self.assertEqual("骰子构筑 P0 v0.7 — "+label,publishing.release_title(manifest))
                self.assertEqual(before,manifest)

    def test_missing_or_unknown_native_scope_never_invents_a_diagnostic_title(self):
        for native in (None,"PASSED","NOT_RUN_ON_MAC",""):
            with self.subTest(native=native):
                manifest={"reviewCandidate":True,"allowFailedWindowsPerformance":True,"windowsReference":{"windowsNativeDisplayStatus":native}}
                with self.assertRaises(ValueError):publishing.release_title(manifest)


if __name__=="__main__":unittest.main()
