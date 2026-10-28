# 本地证据完整性检查工具

`quality_check.py` 是独立编写的标准库 Python 3.11 工具。它只读本地文件，不调用引擎、网络或下载的上游脚本，不修改任务状态。目前尚未接入 CI。

输出 `CHECKS_PASSED` 表示本次声明的结构、文件哈希或测试报告条件满足；它不证明证据真实产生于某次运行，不认证审核人或人工授权，不证明美术品质、游玩体验、需求覆盖完整，也不代表游戏任务被接受。源码与证据同时被重写时，哈希检查无法识别伪造；独立运行、复核和权限控制仍必需。

## 调用方法

从项目根目录运行，路径参数均为项目根内使用 `/` 的相对路径。可在子命令前加 `--root PATH` 指定另一个根目录，供独立测试或检查副本使用。

```powershell
python tools/quality_check.py sources --manifest references/sources.lock.json
python tools/quality_check.py snapshot evidence/task-001/manifest.json
python tools/quality_check.py nunit evidence/task-001/results.xml --min-tests 2 --require-test Game.Move --require-test Game.Save
python -m unittest discover -s tests -p test_quality_check.py -v
```

退出码：`0` 为本次完整性检查满足，`1` 为不满足或无法读取/解析，`2` 为命令行用法错误。常规检查输出一行 JSON，包含 `check`、`integrity_only: true`、`status`，成功时带检查数量，失败时带 `errors`。当前按首个失败返回，不暗示其余条件均已检查。

## 上游来源清单

`sources --manifest` 接受以下格式。示例中的占位值不可通过检查。

```json
{
  "schema_version": 1,
  "sources": [
    {
      "id": "source-name",
      "url": "https://github.com/owner/repository",
      "commit": "实际的40位Git提交哈希",
      "license_path": "references/vendor/source-name/LICENSE",
      "files": [
        {
          "upstream_path": "LICENSE",
          "path": "references/vendor/source-name/LICENSE",
          "sha256": "本地文件实际的64位SHA-256"
        }
      ]
    }
  ]
}
```

来源和文件数组必须非空，来源 ID 和解析后的本地路径不得重复，license 文件必须非空并同时列入该来源的哈希文件清单。`commit` 是固定提交标识，工具不会联网验证 URL、提交与文件内容的对应关系，也不判断许可证是否足以授权具体用途。需要由来源审查记录补足这些事实。

## 输入与产物快照

`snapshot` 验证已声明的输入和产物当前仍与采集时哈希相符。

```json
{
  "schema_version": 1,
  "inputs": [{"path": "docs/design/feature.md", "sha256": "实际的64位SHA-256"}],
  "artifacts": [{"path": "evidence/task-001/results.xml", "sha256": "实际的64位SHA-256"}]
}
```

两个数组、每个文件都必须非空。不允许重复路径、空哈希、占位哈希、绝对路径、父目录跳转或解析后逃出 root 的链接。输入发生变化、产物被篡改或缺失都会失败。检查只覆盖清单明确列出的文件，清单本身不能证明依赖列举完整；复核者必须核对任务影响范围。

## NUnit 报告

支持 NUnit 2 的 `test-results` 和 NUnit 3 的 `test-run`。本工具只接受所有明细均明确通过的报告，并检查根摘要及已提供的 NUnit 3 suite 摘要与实际明细一致。失败、错误、跳过、未知结果、零测试、缺失明细、空报告、重复完整测试名称、计数不一致及缺少必测名称均失败。NUnit 2 用 `name`，NUnit 3 用 `fullname` 匹配 `--require-test`。

XML 在解析器层拒绝 DTD，包括 UTF-16 编码中的声明；不依赖 ASCII 字节扫描。合法 UTF-8、UTF-16 报告可读取。命名空间元素或属性、未知元素、非法父子位置、元数据中的结果属性均保守拒绝，不静默略过。

支持的测试结构是 NUnit 3 的 `test-run → test-suite → test-case`（suite 可继续包含 suite），以及 NUnit 2 的 `test-results → test-suite → results → test-case`（results 可包含下一层 suite）。case 不能直接放在根节点，也不能藏在 environment、output、properties 等元数据内。标准的环境、属性、原因和文本输出可存在于对应位置；NUnit 3 还支持过滤器、设置、附件和断言记录。断言记录有非通过或未知结果同样失败。非标准扩展需要单独审查和增加兼容用例后才能支持。

NUnit 3 的 `testcasecount` 可因过滤而大于本次运行数量；本工具要求它不少于已记录明细，要求 `total` 与实际明细相等。使用 `--min-tests` 和完整的 `--require-test` 清单避免只跑了一小部分测试却被误认为覆盖完成。必要测试集合应在执行前确定，不按现有报告倒填。

NUnit 2 要求根级 `total/errors/failures/not-run/inconclusive/ignored/skipped/invalid` 摘要，以及 case/suite 的 `result/executed/success`；NUnit 3 要求根级 `result/testcasecount/total/passed/failed/inconclusive/skipped` 和 case 的结果与完整名称。缺少所需字段或遇到上述未支持结构返回失败，由人工判断是否需增加经测试的适配，不能静默假定成功。这不是完整的 XSD 校验器，也不会解释所有可选属性。相同完整名称的合法重复测试需先在生成端提供明确的唯一身份；目前保守拒绝歧义。

报告检查不启动测试，不核对报告与某次引擎进程的来源，不判断测试断言是否合理。应与实际命令日志、构建版本、快照及独立复核一起使用。测试文件中的 XML 是人工构造的检查器用例，不是真实游戏测试结果。

格式依据：[NUnit 官方 XML 格式](https://docs.nunit.org/articles/nunit/technical-notes/usage/Test-Result-XML-Format.html) 与 [计数说明](https://docs.nunit.org/articles/nunit/technical-notes/usage/Counting-Tests.html)。
