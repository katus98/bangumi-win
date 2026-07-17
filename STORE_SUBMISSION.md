# Microsoft Store 提交准备

本文件记录番喵当前包对应的 Partner Center 填写建议。测试账号凭据应只填写在 Partner Center 的认证说明中，不得提交到仓库或公开 Issue。

## 商店链接

- 隐私政策 URL：`https://github.com/katus98/bangumi-win/blob/main/PRIVACY.md`
- 支持 URL：`https://github.com/katus98/bangumi-win/issues`
- 内容准则 URL：`https://github.com/katus98/bangumi-win/blob/main/CONTENT_GUIDELINES.md`
- 项目 URL：`https://github.com/katus98/bangumi-win`

提交前应先将本次改动合并到 `main`，确认以上公开 URL 无需登录即可访问。

## 产品声明

- 商店描述和截图应明确写明“Bangumi 非官方第三方客户端”，不得暗示 Bangumi 官方授权或背书。
- 年龄分级问卷应如实声明应用提供在线内容和用户生成内容，不面向儿童。不要仅依据默认过滤而隐瞒 UGC 或潜在敏感内容。
- 商店元数据、图标和截图不得包含 NSFW、露骨或具有误导性的内容。截图建议使用普通条目和测试账号。
- 可用范围只选择 Windows PC。包清单已仅声明 `Windows.Desktop`，不应勾选 Xbox 或 HoloLens。

## 认证说明建议

可在 Partner Center 的认证说明中填写：

> 番喵是 Bangumi 非官方第三方客户端。应用需要使用现有 Bangumi 账号的 access token 登录；测试 token 已在本认证说明的机密字段中提供。Token 经 `/v0/me` 验证后仅存入 Windows Credential Locker，并且只发送到 `api.bgm.tv`。应用默认隐藏 Bangumi 标记为 NSFW 的条目，用户必须在“设置 > 内容安全”中二次确认后才能显示。时间胶囊和条目吐槽属于 UGC，每条内容均提供“举报并在本机隐藏”按钮，内容准则与隐私政策可从设置页访问。

不要把真实测试 token 粘贴到上述仓库文件、商店公开描述或截图中。

## `runFullTrust` 说明

番喵使用 WinUI 3 / Windows App SDK 的桌面进程模型，因此 MSIX 清单保留 `runFullTrust`。应用仅执行其桌面客户端功能，不安装服务、驱动、浏览器扩展或后台常驻组件。若认证表单要求说明受限能力，可使用此理由。

## 提交前检查

- 使用 Release 配置生成 x86、x64、ARM64 包或 MSIX bundle，并在干净的 Windows 11 环境安装验证。
- 确认登录、退出登录、搜索、收藏、详情、时间线、NSFW 开关、举报与本地隐藏均可用。
- 确认隐私政策、内容准则、支持页和举报页从未登录浏览器可访问。
- 在 Partner Center 填写准确的年龄分级、隐私政策 URL、支持信息、认证说明和测试凭据。
- 提交前运行 Windows App Certification Kit，并处理所有失败项。
