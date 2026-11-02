# 发行标题候选独立复核

结论：可集成，限于本次 publisher 标题修复与新增 2 项离线测试；未发现阻断。复核者 /root/v07_run_review 只读候选与生产、只写本 cache，未使用凭据、网络、发布、Unity 或 Player。

四类标题与已验证 manifest 范围相符：一般测试候选、native 待解决限制候选、native 待解决且性能失败诊断候选、同份完整 strict 的自动显示检查通过但物理事件待验且性能失败诊断候选。后一类没有声称 fullWindows、Mac、物理场景或整版通过；unknown/missing native 状态在诊断分支被拒绝。

源前/后 SHA 与作者 bindings 相符。AST 比对仅新增 release_title，既有函数仅 main 有变化；release_inputs、ensure_publish_allowed、release_body 及全部其它既有函数 AST 不变。main 在原 release_inputs 和发布许可校验之后、凭据获取之前生成 title。新建草稿 POST 与最终发布 PATCH 均写同一 title，旧草稿不会在最终发布时留下已过期标题。未修改 manifest、正文、性能/GPU 门槛、CSV/trace/config/build 绑定或上传范围。

独立真实离线运行：原 131 项工具测试 + 新 2 项 = 133/133（2.014 秒）；仓库 20/20（0.430 秒），均零失败/错误/跳过。包含未知/缺失 native 状态拒绝、四标题与输入不变，以及既有诊断/发布失败负例。仅新标题 helper 不独立验证整个 manifest；实际 CLI 仍必须先通过未改的 release_inputs，不能调用 helper 冒充验收。

首轮审计脚本在测试全部通过后对 AST 广度遍历的 POST/PATCH 顺序作了不必要假设；随后按集合精确校验两处调用通过，未修改产品或测试来修正此审计脚本问题。

验证日志和 verification.json 保留在同目录。候选 publisher SHA dd1608d1c11f52b66498a326c5571f91a6732624e203d14bef5a351958a4c4b2；候选 test SHA dd110bf0e47bb20577363d905c9e588c34918302679016757510b6f3b76e4edd。此结论不是网络发布或 Player 验收。
