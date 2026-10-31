# ShopOffer 空分支持久化哈希返工

输入：Player07 的真实 `debug-refresh-shop-save/debug-run.json`。独立定位见 `evidence/v06/player-support/shop-refresh-audit/run01.log`：实际刷新已保存正确报价、钱包、操作序号和随机流；Unity 将非 controller/new_die 报价的 null 分支还原成完整默认对象，导致 StateHash 与冻结回执不一致。原失败证据保留。

修复文件：

- Core/Session/GameSession.Debug.cs：仅在不使用该分支的报价类别下，且所有身份、状态、数据字段均为默认空占位时，canonical投影为null。哈希函数不修改原对象。
- Core/Session/GameSession.Validation.cs：非活动分支允许null或上述完整空占位；任何额外身份、附魔、基金、骰面、寿命或版本数据仍明确拒绝。活动分支即使空白也不归一化。
- Tests/EditMode/Session/V06ShopCanonicalTests.cs：18个普通/异常边界，包含16种非空或非法载荷、活动空载荷与刷新前后一致性。
- Tests/EditMode/Persistence/V06ShopPersistenceTests.cs：两组真实Unity JsonUtility/AtomicSaveStore回归，三轮收费3/5/7或0/5/7、报价序列与完整payload、钱包/序号/三随机流、当前回执StateHash以及重开重复命令不写盘。

托管检查 `shop-canonical-managed02.json/.log` 为 **258/258**，不是Unity运行。01日志保留了.NET首次尝试写用户目录被拒；改用工作区内独立DOTNET_CLI_HOME后02实际完成。没有修改Probe断言、旧测试期待、计分/报价规则或门槛。

root应串行执行新Persistence两例及完整受影响Unity回归，并让Player P19重跑。Core/新测试写锁已释放；源哈希见 `shop-canonical-source.json`。本交接不能将真实Unity或Player尚未运行的项目签为通过。

独立复核追加：原helper还允许手工构造的faces=null空骰对象，但其克隆合同需要数组；root授权后已将非活动骰占位精确限定为Unity实际生成的faces=[]。正常offer.die=null仍合法，活动新骰仍完整校验。新增die-null-faces负例，未改Rules.Clone或其他范围。`shop-canonical-managed03.json/.log` **259/259**，新增组19项；旧258与原失败记录保持不变。此追加源绑定 `shop-canonical-source02.json`，待root合入下一冻结源与真实Unity复验。
