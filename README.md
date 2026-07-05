# bangumi-win

An unofficial https://bgm.tv app client for Windows 11. 一个适配 Windows 11 新 UI 的 Bangumi 第三方客户端。

## 技术方案

- UI 框架：WinUI 3 + Windows App SDK，使用 `NavigationView`、`MicaBackdrop`、`CommandBar`、`InfoBar`、`ListView` 等 Windows 11 原生控件。
- 平台与分发：单项目 MSIX，保留 Microsoft Store 上架所需的 `Package.appxmanifest`，并声明 `internetClient` 网络能力。
- 运行时：.NET 8，C# nullable enabled。
- API：统一通过 `BangumiApiClient` 访问 `https://api.bgm.tv`，请求携带 Bangumi 要求的 User-Agent 与 Bearer token。
- 登录持久化：`TokenStore` 使用 `ApplicationData.Current.LocalSettings` 保存 access token，实现重启后的登录状态恢复。

## 当前功能

- 登录页：粘贴 access token 后调用 `/v0/me` 验证，支持验证当前登录与退出登录。
- 首页：读取当前用户并调用用户时间线 API，以时间倒序展示时间胶囊。
- 收藏页：支持按条目类别筛选动画、书籍、音乐、游戏、三次元，按收藏状态筛选；支持分页浏览。
- 收藏管理：可更新收藏状态；当 API 返回 `eps` 或 `volumes` 时，可更新章节或卷数进度。
- 搜索页：支持全部、动画、书籍、音乐、游戏、三次元、人物搜索；条目结果可进入详情页或加入收藏。
- 条目详情页：展示封面、标题、原名、类型、评分、简介，并支持加入收藏。

## 代码结构

```text
Bangumi.Win/
  Models/       Bangumi API DTO、筛选选项与 UI 展示模型
  Services/     API client、token 持久化、应用级服务入口
  Views/        登录、首页、收藏、搜索、条目详情页面
  MainWindow.*  WinUI 3 NavigationView 应用外壳
```

## 关键设计

`BangumiApiClient` 是唯一的 API 出口，页面不直接拼装 HTTP 请求。这样后续可以在服务层集中补充 OpenAPI 生成客户端、重试、限流、图片缓存和错误映射。

收藏管理按 Bangumi API 的能力实现：条目收藏状态通过 `/v0/users/-/collections/{subject_id}` 更新，章节/卷数进度使用收藏对象上的 `ep_status` 与 `vol_status` 管理。更细粒度的单集状态可以在后续接入 episodes 相关端点后扩展为专门的进度页面。

当前 token 存储优先满足开发阶段的登录态持久化。上架前建议将 `TokenStore` 替换为 Windows Credential Locker 或 DPAPI 封装，并补充隐私声明。

## 开发验证

```powershell
dotnet build
```

当前 `develop` 分支已通过 `dotnet build`，无警告、无错误。
