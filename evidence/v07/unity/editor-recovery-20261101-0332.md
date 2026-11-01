# Editor interruption, 2026-11-01 03:32 UTC

Unity 6000.6.7f1, Windows Editor PID40096, branch codex/v07-art-and-ui, uncommitted art0.7.0 changes. A PLAY_ART07_ScoreLabels rerun after the FinalScore alignment patch was interrupted by a native Editor crash. No successful NUnit result was produced for that invocation. Existing completed NUnit reports remain separate evidence.

The retained native stack enters MimallocPrimErrorHandler / MemoryManager::Allocate / VertexData::AllocateMemory / Mesh::Clear / uGUI VertexHelper::FillMesh. An independent agent reported an “Out of memory” tool failure around the same time. This supports a memory-allocation failure, but does not establish whether the Editor, concurrent tooling, a resource leak, or another condition exhausted available memory. No claim of an external-only cause is made.

Raw dump and limited log tail are retained locally under artifacts/cache/v07-crash-033212. The full Editor startup log is not published because it may contain account launch parameters. After the crash, Win32_OperatingSystem reported total/free physical memory 67,028,028 / 53,610,912 KiB and total/free virtual memory 99,028,172 / 76,586,596 KiB; these are post-crash values, not the failure-time usage.

The same project reopened in batchmode as PID23240. The first restored test batch, nunit-20261101-033436.xml, failed 0/3: no GameView had been recreated, yielding default640×480 and actual end-of-frame capture timeouts. These failures were retained. Root created and focused the actual GameView with the public EditorWindow API and restored the existing16ms Pipeline auto-tick setting. No screenshot substitutes or assertion changes were used.

Subsequent actual strict tests passed: nunit-20261101-033545.xml at1920×1080,3/3; nunit-20261101-033650.xml at1280×720,3/3. Each covers100/125/150% fonts and actual GPU comparisons. The latter process private memory was observed at approximately2.88GiB after the runs, with a short recovery sample in the cache. These limited observations do not resolve the original crash or replace full-suite, Player, or sustained memory validation.

Open follow-up: observe the complete final test batch and actual Player pressure run, retain process-memory evidence, and investigate reproducible growth rather than relying on periodic restarts as a fix. The separate source-list spacing and transaction snapshot visual defects remain under repair.
