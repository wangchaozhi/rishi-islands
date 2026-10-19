# 日噬群岛 Rishi Islands

一款用 Unity 6 开发中的第一人称荒岛探险恐怖游戏。主角林舟为寻找失踪的父亲，漂流到南太平洋一片「太阳会生病的海」，在七座岛屿上追寻失落文明的「太阳之心」，也一步步揭开家族与黑日之声的秘密。

> 开发中的原型：目前完成了第一座岛「晨星岛」的地形、植被、昼夜、声音、剧情线和两段过场动画。

| | |
|---|---|
| ![沙滩与沉船](Docs/images/01_spawn_wreck.jpg) | ![晨星岛航拍](Docs/images/12_aerial.jpg) |
| ![神殿](Docs/images/03_temple_far.jpg) | ![林下灌丛](Docs/images/13_undergrowth.jpg) |
| ![夜月](Docs/images/15_moon_night.jpg) | ![清晨残月](Docs/images/16_morning_moon.jpg) |

## 特性

- URP（Forward+）画面：程序化天空（太阳、带月相的月亮、星空、云）、Gerstner 海浪、体积光、电影级后期
- 程序化生成的热带雨林岛：椰子树、林冠大树、芭蕉、海芋、红树林、高大灌丛、溪流、海崖、沼泽
- 昼夜系统（醒来于清晨 7 点、晨雾、拿到碎片后太阳异常坠落）
- 分层环境声、按地表切换的脚步声、命中声
- Timeline + Cinemachine 过场（CG01 风暴、CG02 醒来）与自定义轨道
- 剧情系统：笔记日志、目标链、符文石柱谜题、宝箱与碎片
- 小动物生态：螃蟹、蝴蝶、鱼群、蝙蝠、萤火虫、鸟群

设计文档在 [Docs](Docs)：[剧情设计](Docs/剧情设计.md)、[怪物图鉴](Docs/怪物图鉴.md)、[开发路线](Docs/开发路线.md)、[架构说明](Docs/架构说明.md)。

## 环境

- Unity **6000.3.26f1**（6.3 LTS）
- Python 3.10+，带 `numpy` 和 `Pillow`（下载美术资源用）
- Git LFS

## 开始

仓库里不包含体积大的第三方资源，克隆后需要补齐：

```bash
git lfs install
git clone https://github.com/wangchaozhi/rishi-islands.git
cd rishi-islands
python Tools/AssetFetch/fetch_all.py
```

`fetch_all.py` 从 [Poly Haven](https://polyhaven.com)（CC0）下载模型、地表贴图和 HDRI 到 `Assets/Art`，大约 1.2 GB。

然后在 Unity Asset Store 免费获取这两个资源包，并用 Package Manager 导入到项目里（许可不允许放进公开仓库）：

- **Terrain Sample Asset Pack**（Unity Technologies）：导入后如果自动装上了 HDRP，请在 Package Manager 里移除，项目使用 URP
- **Living Birds**（不导入也能运行，只是没有鸟群）

最后生成场景（也可以在编辑器里用菜单 `Tools/Island`）：

```bash
Unity.exe -batchmode -quit -projectPath . -executeMethod Rishi.Builders.IslandPreview.BuildAndCapture -logFile build.log
```

打开 `Assets/Scenes/Island.unity` 按 Play。操作：WASD 移动、Shift 冲刺、空格跳、鼠标左键射击、R 换弹、E 互动、J 日志、按住 T 快进时间、Esc 释放鼠标。

## 代码结构

所有代码在 `Assets/_Game/`，按模块分程序集，命名空间 `Rishi.<模块>`，依赖单向：

```
Core ← World ← Audio ← Player ← Weapons ← UI
                  ↑  ← Story / Cutscene / Wildlife
Rendering（独立）     Builders.Editor（编辑器：程序化生成场景）
```

详见 [架构说明](Docs/架构说明.md)。运行测试：

```bash
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml
```

## 素材来源

- 模型、贴图、HDRI：[Poly Haven](https://polyhaven.com)，CC0
- 手枪模型：Poly Haven `service_pistol`，CC0
- 植物网格与贴图、天空、海水、溪流、体积光着色器：项目内程序化生成
- 音效：目前为程序合成的临时音效（`Assets/Audio/Placeholder`）
