# bangumi-win

An unofficial https://bgm.tv app client for Windows 11. 一个适配 Windows 11 新 UI 的 Bangumi 第三方客户端。

## 技术方案

- UI 框架：WinUI 3 + Windows App SDK，使用 `NavigationView`、`MicaBackdrop`、`CommandBar`、`InfoBar`、`ListView` 等 Windows 11 原生控件。
- 平台与分发：单项目 MSIX，保留 Microsoft Store 上架所需的 `Package.appxmanifest`，并声明 `internetClient` 网络能力。
- 运行时：.NET 8，C# nullable enabled。
- API：统一通过 `BangumiApiClient` 访问 `https://api.bgm.tv`，请求携带 Bangumi 要求的 User-Agent 与 Bearer token。
- 登录持久化：`TokenStore` 优先使用 Windows Credential Locker 保存 access token，并保留 `ApplicationData.Current.LocalSettings` 回退，实现重启后的登录状态恢复。

## 当前功能

- 登录页：粘贴 access token 后调用 `/v0/me` 验证，支持验证当前登录与退出登录。
- 首页：读取当前用户后加载 Bangumi 网页时间胶囊，以时间倒序展示动态。时间胶囊当前不在 v0 OpenAPI 中，因此解析逻辑被隔离在服务层。
- 收藏页：支持按条目类别筛选动画、书籍、音乐、游戏、三次元，按收藏状态筛选；支持分页浏览。
- 收藏管理：可更新收藏状态；书籍可更新话数/卷数进度，动画可进入单集进度页批量标记想看、看过、抛弃或未收藏。
- 搜索页：支持全部、动画、书籍、音乐、游戏、三次元、人物搜索；条目和人物结果可进入详情页，并支持加入对应收藏。
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

收藏管理按 Bangumi API 的能力实现：条目收藏状态通过 `/v0/users/-/collections/{subject_id}` 更新。OpenAPI 明确说明直接修改 `ep_status` 与 `vol_status` 只适用于书籍，因此动画进度使用 `/v0/users/-/collections/{subject_id}/episodes` 读取和批量更新单集状态。

时间胶囊当前未出现在 Bangumi v0 OpenAPI 中，客户端使用 `https://bgm.tv/user/{username}/timeline` 的只读 HTML 作为数据源，并把 HTML 解析封装在 `BangumiApiClient` 内。后续如果官方提供 v0 时间线端点，只需要替换服务层实现。

MSIX 清单保留 WinUI 3 桌面应用所需的 `runFullTrust`，并移除了模板里与本客户端无关的系统 AI 权限。商店上架前仍需要替换正式 Publisher、包名、图标和隐私声明。

## 开发验证

```powershell
dotnet build
```

当前 `develop` 分支已通过 `dotnet build`，无警告、无错误。
