# MajdataView_web — `nolist` 分支分析文档

## 概述

`nolist` 分支是 [FANdoraxxx/MajdataView_web](https://github.com/FANdoraxxx/MajdataView_web) 的一个功能分支，其核心改动是**移除了歌曲选择列表（Song List）**，将查看器改造成了一个可嵌入 Next.js 等 Web 框架的 WebGL 组件。

---

## 与 `master` 分支的主要差异

### 1. 移除了歌单功能（核心改动 `86cec73 去除歌表`）

| 已移除 | 说明 |
|---|---|
| `Assets/Scenes/SongLstMenu.unity` | 歌单选择场景 |
| `Assets/Scripts/SelectManu/SongSelect.cs` | 歌单 UI 脚本 (231 行) |
| `Assets/Scripts/SelectManu/PlayButton.cs` | 歌单播放按钮 |
| `Assets/Scripts/SelectManu/loadingSpanning.cs` | 歌单加载动画 |
| `Assets/Scripts/ApiRoot.cs` | API 根路径配置 (148 行) |
| `Assets/Scripts/SongInformation.cs` | 歌曲信息持有类 |
| 部分字体资源 | `CourierPrime-Regular`、`SweiGothicCJKsc-Bold` |

### 2. 新增的核心文件

| 新增文件 | 说明 |
|---|---|
| `Assets/HandleJSMessages.cs` | 接收来自外部 JS/网页应用的消息，触发加载流程 |
| `Assets/Scripts/WebLoader.cs` | 通过 HTTP 下载背景图并设置到 `BGManager` |

---

## 运行架构

### 数据流程

```
[外部 Next.js 应用 / JS 宿主页面]
        │
        │ (通过 Unity SendMessage API)
        ▼
HandleJSMessages.ReceiveMessage(string message)
        │   message 格式（换行分隔）：
        │   maidata_url\ntrack_url\nbg_url\nmv_url\nlevel
        │
        ▼
GameMainManager.WebLoad(chartpath, bgpath, audiopath, videopath, level)
        │
        ├── SimaiDataLoader.initFromWeb(chartpath)   ← 下载并解析 maidata.txt
        ├── SoundEffect.LoadWebAudio(audiopath)      ← 下载 BGM 音频
        ├── WebLoader.LoadBGFromWeb(bgpath)          ← 下载背景图片
        └── BGManager.videoPlayer.Prepare(videopath) ← 准备 MV 视频（可选）
```

### 加载状态机

`WebLoad` 中使用 `status` 计数器追踪 4 个异步资源的完成情况：
- `status >= 4` 时触发 `SetReadyMode()`，允许用户开始播放

---

## 关键文件详解

### `HandleJSMessages.cs`
- **用途**：Unity 与外部网页之间的桥接器
- **`ReceiveMessage(string message)`**：由宿主 JS 页面通过 `SendMessage` 调用，解析 `\n` 分隔的 URL 字符串，调用 `GameMainManager.WebLoad()`
- **`UnityLoaded()`**：WebGL 加载完毕后通知宿主 JS（通过 `DllImport("__Internal")`）
- **编辑器调试**：在 Unity Editor 中 `Start()` 会自动加载一个测试谱面（`majdata.net` API 接口）

### `GameMainManager.cs`
- **`WebLoad(chartpath, bgpath, audiopath, videopath, level)`**：接受明确的资源 URL 而非从 `SongInformation` 读取（这是与 master 的关键接口变化）
- **视频支持**：nolist 分支新增了对 MV 视频的处理（`BGManager.videoPlayer`），视频加载超时时有容错（2 秒超时后继续加载）
- **`status` 计数**：现在等待 4 个资源（chart、audio、bg、video），master 只等待 3 个

### `BGManager.cs`
- 新增 `VideoPlayer` 组件支持
- 视频播放时自动隐藏背景图（`spriteRender.forceRenderingOff`）
- 出错时回退显示背景图

### `WebLoader.cs`
- 静态辅助类，通过 `UnityWebRequest` 下载背景图片
- 将下载的纹理转为 `Sprite` 并赋给 `BGManager.spriteRender`

---

## 最新提交的改动（在 nolist 基础上）

| 提交 | 作者 | 说明 |
|---|---|---|
| `9b5a9cc` add non-C touch | LingFeng-bbben | `TouchHoldDrop` 新增对非 C 区域（A/B/D/E）的位置计算支持 |
| `b9d2cdc` remove debug | LingFeng-bbben | 移除调试代码、清理 `SettingsManager` 中的冗余逻辑 |

### non-C touch 区域说明

`TouchHoldDrop.GetAreaPos()` 现在支持五个触摸区域：

| 区域 | 说明 | 距中心距离 |
|---|---|---|
| `C` | 中央 | 0 |
| `B` | 内环 | 2.3f |
| `A` | 外环 | 4.1f |
| `E` | 中间环（旋转偏移） | 3.0f |
| `D` | 外环（旋转偏移） | 4.1f |

---

## 技术栈总结

- **引擎**：Unity（WebGL 构建目标）
- **语言**：C#
- **外部通信**：Unity `SendMessage` + `DllImport("__Internal")` JS 互操作
- **数据格式**：Simai（maimai 谱面格式）
- **资源加载**：`UnityWebRequest`（HTTP）
- **UI 框架**：Unity UGUI + TextMesh Pro
- **动画**：DOTween

---

## 与上游的关系

```
sblzdddd/MajdataView (原版，含歌单)
    └── Wh1tyEnd/MajdataView_web (WebGL 版)
            ├── master (保留歌单，含 SongLstMenu 场景)
            ├── self-api (使用自建 API 服务器)
            └── nolist (移除歌单，改为 JS 消息驱动，加入视频支持)
                    └── FANdoraxxx/MajdataView_web (fork，在 nolist 基础上继续开发)
```

---

## 总结

`nolist` 分支将 MajdataView_web 从一个**独立的、内置歌单的查看器**改造为一个**可嵌入宿主网页的 WebGL 播放器组件**。宿主页面（如基于 Next.js 的 Web 应用）通过 `SendMessage` 传递资源 URL 来控制播放器加载内容。这使得该项目可以作为更大的在线谱面浏览平台（如 `majdata.net`）的子组件使用。
