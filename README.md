# bangumi-win

An unofficial https://bgm.tv app client for Windows 11. 一个适配 Windows 11 新 UI 的 Bangumi 第三方客户端。

## 技术方案

- UI 框架：WinUI 3 + Windows App SDK，使用 `NavigationView`、`MicaBackdrop`、`CommandBar`、`InfoBar`、`ListView` 等 Windows 11 原生控件。
- 平台与分发：单项目 MSIX，保留 Microsoft Store 上架所需的 `Package.appxmanifest`，并声明 `internetClient` 网络能力。
- 运行时：.NET 8，C# nullable enabled。
- API：统一通过 `BangumiApiClient` 访问 `https://api.bgm.tv`，请求携带 Bangumi 要求的 User-Agent 与 Bearer token。
- 登录持久化：`TokenStore` 使用 Windows Credential Locker 保存 access token；旧版本曾写入 `LocalSettings` 的明文 token 会在启动时迁移并删除。

## 当前功能

- 顶部账号入口：应用右上角展示当前登录状态、头像和昵称；新 token 通过 `/v0/me` 验证成功后才会保存，支持更新账号和退出登录。
- 首页：读取当前用户后加载 Bangumi 网页时间胶囊，以时间倒序展示动态；支持下滑自动加载后续页，点击带作品信息的动态进入作品详情。
- 收藏页：顶部使用下划线样式的动画、书籍、音乐、游戏、三次元切换条筛选条目类别，收藏状态作为子切换条；列表下滑触底自动加载后续数据，点击条目进入统一详情页。
- 搜索页：顶部使用下划线样式的全部、动画、书籍、音乐、游戏、三次元、人物切换条切换搜索分类；结果下滑触底自动加载后续数据，点击结果进入对应详情页。
- 条目详情页：展示封面、标题、原名、类型、评分、简介、当前收藏状态和动画单集状态；点击收藏状态卡片编辑收藏，单集状态可在单集列表内直接修改。
- 设置页：支持跟随系统、浅色和深色主题，并展示当前包版本、项目与 API 文档入口。

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

`AppServices` 负责当前用户会话，统一执行“先验证、后保存”以及 401 自动失效处理。API token 只附加到明确需要鉴权的 `api.bgm.tv` 请求，不会发送到时间胶囊和吐槽所使用的 `bgm.tv` HTML 页面。

收藏管理按 Bangumi API 的能力实现：条目收藏状态通过 `POST /v0/users/-/collections/{subject_id}` 新增或修改，显式使用 `application/json` 请求体以避免媒体类型错误。OpenAPI 明确说明直接修改 `ep_status` 与 `vol_status` 只适用于书籍，因此动画进度使用 `/v0/users/-/collections/{subject_id}/episodes` 读取和批量更新单集状态。

时间胶囊当前未出现在 Bangumi v0 OpenAPI 中，客户端使用 `https://bgm.tv/user/{username}/timeline` 的只读 HTML 作为数据源，并把 HTML 解析封装在 `BangumiApiClient` 内。后续如果官方提供 v0 时间线端点，只需要替换服务层实现。

时间胶囊解析会提取动态中作品卡片的封面图和作品 ID；没有作品卡片的纯文本动态保持无封面展示且不可进入作品详情。时间胶囊、收藏和搜索列表都使用增量加载；筛选、搜索条件、账号或导航变化会取消旧请求，避免过期结果覆盖当前页面。

MSIX 清单保留 WinUI 3 桌面应用所需的 `runFullTrust`，并移除了模板里与本客户端无关的系统 AI 权限。商店上架前仍需要替换正式 Publisher、包名、图标和隐私声明。

## 开发验证

```powershell
dotnet build
```

当前 `develop` 分支已通过 `dotnet build`，无警告、无错误。
