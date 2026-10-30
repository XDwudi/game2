"""Reviewer boundary checks. Mutated fixtures are NOT Unity execution reports."""
from pathlib import Path
import contextlib
import hashlib
import importlib.util
import io
import json
import types
import unittest
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[3]
OUT=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('v05_delivery_review',ROOT/'tools/v05_delivery.py')
delivery=importlib.util.module_from_spec(spec);spec.loader.exec_module(delivery)
EDIT=ROOT/'evidence/v05/unity/nunit-20261030-053526.xml'
PLAY=ROOT/'evidence/v05/unity/nunit-20261030-053623.xml'
PROOF=OUT/'unity-prebuild-pdb03-save-cache.json'
FIXTURES=OUT/'gate-negative-fixtures-save-cache'
FIXTURES.mkdir(exist_ok=True)

class IndependentGateChecks(unittest.TestCase):
    def test_actual_nunit_reports_are_accepted(self):
        self.assertEqual(delivery.verify_nunit(EDIT,'EditMode',554)['passed'],554)
        self.assertEqual(delivery.verify_nunit(PLAY,'PlayMode',24)['passed'],24)

    def test_root_failure_rejected(self):self.bad_xml('root-failure',lambda r:r.set('result','Failed'))
    def test_inconsistent_passed_total_rejected(self):self.bad_xml('passed-count',lambda r:r.set('passed','553'))
    def test_skipped_rejected(self):self.bad_xml('skipped-count',lambda r:r.set('skipped','1'))
    def test_nonpassing_leaf_rejected(self):self.bad_xml('leaf-failure',lambda r:r.findall('.//test-case')[0].set('result','Failed'))
    def test_duplicate_leaf_name_rejected(self):
        self.bad_xml('duplicate-name',lambda r:r.findall('.//test-case')[0].set('fullname',r.findall('.//test-case')[1].get('fullname')))
    def test_wrong_mode_rejected(self):
        def alter(root):
            for p in root.findall('.//property'):
                if p.get('name')=='platform':p.set('value','PlayMode')
        self.bad_xml('wrong-mode',alter)
    def test_incomplete_suite_rejected(self):
        def alter(root):
            for parent in root.iter():
                child=parent.find('test-case')
                if child is not None:parent.remove(child);return
        self.bad_xml('missing-test',alter)
    def test_empty_pdb_documents_rejected(self):self.bad_proof('empty-documents',lambda p:p.update(documents=[]),'no project source')
    def test_current_source_mismatch_rejected(self):
        self.bad_proof('stale-compiled-document',lambda p:p['documents'][0].update(expectedChecksum='0'*64),'Current source differs')
    def test_dll_changed_after_audit_rejected(self):
        self.bad_proof('changed-dll',lambda p:p['assemblies'][0].update(dllSha256='0'*64),'Recompiled after PDB audit')
    def test_pdb_identity_mismatch_rejected(self):
        self.bad_proof('wrong-pdb',lambda p:p['assemblies'][0].update(pdbMatchesDll=False),'PDB does not match assembly')

    def bad_xml(self,name,mutate):
        tree=ET.parse(EDIT);mutate(tree.getroot());p=FIXTURES/(name+'.fixture.xml');tree.write(p,encoding='utf-8',xml_declaration=True)
        with self.assertRaises(ValueError):delivery.verify_nunit(p,'EditMode',554)
    def bad_proof(self,name,mutate,message):
        proof=json.loads(PROOF.read_text(encoding='utf-8'));mutate(proof);p=FIXTURES/(name+'.fixture.json');p.write_text(json.dumps(proof,indent=2),encoding='utf-8')
        args=types.SimpleNamespace(edit=str(EDIT),play=str(PLAY),pdb=str(p))
        with self.assertRaisesRegex(ValueError,message):delivery.gate(args)

if __name__=='__main__':
    stream=io.StringIO();suite=unittest.defaultTestLoader.loadTestsFromTestCase(IndependentGateChecks)
    result=unittest.TextTestRunner(stream=stream,verbosity=2).run(suite)
    text=stream.getvalue();print(text)
    (OUT/'delivery-gate-independent-save-cache-tests.log').write_text(text,encoding='utf-8')
    summary={'scope':'Independent deterministic gate-script boundary checks; fixture XML/JSON are deliberately altered test data, not actual Unity results',
             'ran':result.testsRun,'failures':len(result.failures),'errors':len(result.errors),'passed':result.wasSuccessful(),
             'productionScriptSha256':hashlib.sha256((ROOT/'tools/v05_delivery.py').read_bytes()).hexdigest()}
    (OUT/'delivery-gate-independent-save-cache-tests.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
    raise SystemExit(0 if result.wasSuccessful() else 1)
