# MT03 Low：代码影响范围补充

**仅静态核对通过；本轮未单独切换 Low 做实机截图/GPU 验证。** 不把默认画质、Grain 或 Reduced 的真实通过移称 Low 通过。

`GameApp.Art07Settings.cs` 的质量设置把 `quality07` 在 0/1/2（低/标准/高）切换，保存偏好并调用 `ApplyArtPreferences07()`。`GameApp.Art07Presentation.cs:47` 的该方法在 Low 时关闭主灯阴影、关闭可选扫描线；颗粒仍由 grain/Reduced 控制。它先取现有 MaterialPropertyBlock，再仅写 `_Grain` 和 `_Scanlines`，没有替换或禁用 Renderer/material/shader/纹理，没有写 `_UseTableMap` / `_TableMap`。表现质量还传给原有效果降级系统，不切换桌面材质。

`Art07Felt.shader` 中桌面采样只受 `_UseTableMap>.5` 控制，与质量级别和 `_Grain` 无耦合；Low 的无实时阴影会改变预期接触阴影，桌面构图本身仍按相同比例分支输出。`Art07MenuReveal` 仅用 property block 改可选 drift/breath，不移除绑定。全 Runtime/UI 对桌面对象与新增两属性的只读搜索未发现另一个质量切换路径。

因此在现有代码下，Low 没有卸载/换掉/关掉新桌面的路径。静态结论有依据，Low 实机项仍标 **未实测**；不新增测试、不改变门槛。其余范围以 `FINAL_REVIEW.md` 为准。
