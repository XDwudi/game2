骰子构筑 v0.72 · 高清简约美术试玩版（rc.2）


这一版统一了界面的画法：纸卡、按钮、面板、空槽和桌面都是边缘干净的高清矢量质感，
木桌是与界面同一风格的窄木沿与毛毡。卡牌会缓慢呼吸，鼠标悬停时朝指针倾斜；
主菜单字标轻轻浮动，毛毡上有一道很淡的光带缓慢移动。71 项内容插画、3D 骰子和全部玩法与 v0.71 相同。

rc.2 相对 rc.1：重做了分数栏和左侧计分板（读数窗、因子色片、本关进度条、加粗数字），
桌沿收窄，所有面板都落在毛毡上；提交槽、待锁标记和次要按钮改为圆角；数值变化时读数会弹一下。

Windows：完整解压后运行 DicePrototype.exe，保留同目录的 DicePrototype_Data、UnityPlayer.dll 和其他文件。

macOS（Universal，Intel 与 Apple 芯片）：完整解压后保留整个 DicePrototype.app。
这个包是在 Windows 上构建和压缩的，没有签名、没有公证，也没有在任何 Mac 上启动过，能否运行尚未验证。
系统很可能拦截首次打开。可在“终端”里依次执行（把路径换成你解压的位置）：
  xattr -cr DicePrototype.app
  chmod +x DicePrototype.app/Contents/MacOS/DicePrototype
  codesign --force --deep --sign - DicePrototype.app
然后右键 DicePrototype.app 选择“打开”。如果仍无法启动，请把提示文字和系统版本反馈给我们。

第一次游玩：点击“新建挑战”，确认种子与初始状态；投掷 → 点选待锁骰 → 确认锁定 → 提交。
确认锁定后，本手不能直接解锁。“继续对局”恢复最后一次成功保存的状态；已有存档时，新建会再次确认覆盖。
默认键位：R 投掷，L 确认锁定，Enter 提交，Backspace 清除待选，H 牌型手册，Esc 暂停或关闭弹窗。
右键查看骰子详情，拖动已锁骰可调整顺序。自定义键位以设置页为准。

如果不喜欢常驻动画：设置 → 操作与辅助 → 打开“减少动态”，卡牌、字标、光带和读数弹跳都会停止。

存档与 v0.71 通用（规则 p0.6.0，存档 schema4）。Windows 的位置在
%USERPROFILE%\AppData\LocalLow\XDwudi\DiceCraft P0。

本版做过的验证：完整 EditMode 与 PlayMode 自动测试（1920×1080 与 1280×720 各一轮）；
用 Windows 包在新目录启动并自动走完一段对局与商店。
本版没有做的验证：macOS 实机启动与运行、Windows 帧时间与独占全屏专项（v0.7 起即未通过，本版未重测）、
独立复核。风格与体验由试玩者判断。AudioMixer 总线仍未接入，声音使用分类音量回退。

请反馈页面、分辨率、文字比例、复现步骤和截图。完整身份见游戏内“版本与构建”。
