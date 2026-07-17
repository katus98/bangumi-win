# 番喵隐私政策

生效日期：2026 年 7 月 18 日

番喵（以下简称“本应用”）是由 katus 独立开发的 Bangumi 非官方第三方客户端。本应用与 Bangumi 官方不存在隶属、授权或背书关系。本政策说明本应用访问、使用、保存和披露信息的方式。

## 1. 本应用访问的信息

当你使用 Bangumi access token 登录时，本应用会通过 Bangumi API 访问完成核心功能所必需的信息，包括 Bangumi 用户名、昵称、头像、收藏、观看或阅读进度、时间胶囊动态以及条目吐槽。上述信息由 Bangumi 提供，并受 Bangumi 发布的适用规则约束。

本应用不会要求你的 Bangumi 密码。请勿将 access token 填写到举报、反馈、截图或其他公开内容中。

## 2. 信息如何使用与传输

- Access token 仅通过 HTTPS 发送到 `api.bgm.tv`，用于验证账号和执行你主动发起的 Bangumi 操作。
- 时间胶囊和条目吐槽通过 HTTPS 从 `bgm.tv` 读取；本应用不会在这些网页请求中附加 access token。
- Bangumi 用户资料、收藏、进度和内容仅用于在本应用中提供对应功能。
- 封面和头像可能由 Bangumi 页面返回的图片主机提供。加载图片时，相应主机可能按其政策处理 IP 地址、设备网络信息和请求日志。

## 3. 本机保存的信息

- Access token 保存在 Windows Credential Locker 中，不以明文写入应用设置。
- 主题、是否主动显示 NSFW 条目等偏好保存在 Windows 应用本地设置中。
- 你隐藏的动态或吐槽仅以不可逆哈希标识保存在本机，最多保留 100 项；本应用不保存被隐藏内容的正文副本。

这些信息不会同步到番喵开发者的服务器。本应用当前不运营自有后端，不使用广告 SDK、行为分析 SDK 或跨应用跟踪技术，也不出售个人信息。

## 4. 举报与第三方服务

当你选择“举报给开发者”时，本应用会打开 GitHub 举报页面，并预填条目 ID 和不可逆的本地内容标识。只有在你确认并提交后，GitHub 才会收到你填写的信息；该过程受 [GitHub 隐私声明](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement)约束。

你也可以选择打开 Bangumi 网站，通过 Bangumi 的官方渠道处理源内容。番喵只能在本应用内隐藏内容，无法直接删除 Bangumi 服务器上的内容。

## 5. 保留与删除

- 在账号对话框中退出登录会删除 Credential Locker 中的 access token。
- 可在“设置 > 内容安全”中清除所有本地隐藏记录。
- 卸载本应用会由 Windows 删除应用本地设置；Credential Locker 数据的生命周期由 Windows 管理，也可通过退出登录主动删除。
- Bangumi 和 GitHub 保存的信息需分别依据其政策和账号工具删除。

## 6. 内容安全与未成年人

Bangumi 可能包含用户生成内容和被标记为 NSFW 的条目。本应用默认隐藏 Bangumi 标记为 NSFW 的条目，只有用户明确确认后才会显示。本应用不面向儿童，也不应由未成年人自行开启成人或敏感内容。

## 7. 安全与政策变更

本应用采取与其规模和数据类型相适应的措施，例如 HTTPS、Windows Credential Locker、最小化保存和不运营自有数据服务器。但任何网络传输或存储都无法保证绝对安全。

本政策发生实质变化时，将通过本仓库更新本文件及生效日期。继续使用更新后的版本即表示你已阅读新版政策。

## 8. 联系方式

隐私问题、数据请求或内容举报可通过 [GitHub Issues](https://github.com/katus98/bangumi-win/issues) 联系维护者。提交时请勿包含 access token、密码或其他敏感个人信息。
