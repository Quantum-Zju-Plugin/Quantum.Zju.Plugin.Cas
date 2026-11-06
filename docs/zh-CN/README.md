# 浙江大学 CAS 账号插件

[English](../en/README.md) · [术语表](glossary.md) · [集成合约](integration.md) · [发布记录](releases.md)

`quantum.zju.plugin.cas` 提供浙江大学 [CAS](glossary.md#cas) 账号连接，向 `quantum.settings/accounts` 贡献账号区域，并为外部集成插件获取绑定目标服务的认证回跳地址。使用已发布的 [SDK](glossary.md#sdk) 0.3.0-rc.3。Desktop 宿主必须实现该设置扩展点；`.ci/desktop-revision.txt` 固定集成测试使用的公开宿主提交。

## 登录

打开**设置 → 账号 → 浙江大学 CAS 账号**，选择**登录**。原生 [WebView](glossary.md#webview) 打开学校 CAS 登录页面，由学校处理密码、跳转及额外验证。检测到 CAS 回跳并验证成功后，窗口自动关闭，设置页显示已验证的账号标识。重新进入设置页仍会显示当前账号。按钮沿用宿主设置页样式，无需额外手动确认。

学校页面提示“认证登录页面闲置时间过长，请打开新页面，F5刷新无效”时，浏览器会自动重新发起一次登录，丢弃失效的流程地址，使用新的登录地址并保留 CAS Cookie。若新页面仍然超时，可点击**重新打开**再次发起登录，避免自动跳转循环。检测逻辑只读取页面显示的错误文案并返回布尔值，不读取表单内容。

Windows 使用 WebView2，Mac Catalyst 使用 WKWebView。通过原生 Cookie 管理器获取仅适用于 CAS 认证源的 Cookie，包括 [HttpOnly](glossary.md#httponly) Cookie，同时保留浏览器 [User-Agent](glossary.md#user-agent)。接受连接前，插件会为 CAS 门户本身获取服务票据，并通过 CAS 的 `serviceValidate` 接口验证票据、取得账号标识，不依赖任何教务系统完成登录验证。验证失败时窗口保留，并显示重试操作。替换失败或取消会保留原连接；仅凭 HTTP 200 或存在 Cookie 不能证明登录成功。

设置页只保留一个操作：未登录时显示**登录**，登录后显示账号标识和**退出**。使用其它账号时，先退出再登录。页面导航前，原生 Cookie 管理器会清理适用于 CAS 认证源的 Cookie，包括旧版遗留的单点登录状态；登录使用标准服务流程，不添加强制重新认证参数。验证登录成功后会再次清理这些浏览器 Cookie；提供者独立的内存 HTTP Cookie 快照仍可用于获取服务票据，避免下一次登录自动回到原账号。其它主机和路径的 Cookie 保持不变。

本插件实现 CAS，不是 [OIDC](glossary.md#oidc)，不需要注册 OIDC 客户端。未启用第三方身份提供方的跨域跳转，请使用学校账号或学校域名内的验证流程。

## 职责与凭据

提供者只负责原生登录、内存中的 CAS 浏览器 Cookie、当前本地连接和票据获取。通过 [RPC](glossary.md#rpc) 暴露 `zju.cas.account` 与 `zju.cas.service-ticket` 两项能力，详见[集成合约](integration.md)。

外部插件选择其已注册的服务地址、消费一次性回跳地址、建立和清理各自站点的 Cookie 或令牌，并实现业务请求与数据解析。提供者不跟随服务回跳、不读取业务接口、不解析教务记录，也不提供通用 HTTP 代理。站点特有的登录链由外部集成负责。

CAS Cookie 和密码不会进入贡献数据或 RPC 返回值。服务票据回跳地址则是有意交给请求者的短期凭据，只能立即使用一次，不得记录、持久化或共享给无关代码。已安装插件是本地受信任客户端，这不是匿名公共接口。服务地址必须获得学校认可；提供者还限制为浙大域名下默认端口的 HTTPS 地址，禁止用户信息、片段及预置 ticket 参数。

每个插件运行时支持一个本地账号连接。不透明的 `sessionId` 标识连接代次，不代表学校身份主体或已验证的学号；替换账号会变化，退出或插件重新加载后清除。账号能力只报告本地状态，学校会话过期后，下一次票据请求仍可能失败。**退出**会释放提供者的 HTTP 会话并清除账号信息。登录浏览器不保留已成功交接的 CAS 会话，因此再次登录会显示新的登录页。外部插件须分别清理自己的站点会话，不会因此在其它浏览器执行 CAS 全局退出。

## 消费方兼容性

0.5 版使用 `quantum.zju.plugin.cas` 插件 ID 及上述两项 CAS 能力。原 `quantum.zju.plugin.oidc` / `quantum.plugin.zjuam` 提供者和 `zjuam.read-resource` 合约不作为别名保留。已有消费方须先采用新合约并自行持有业务读取实现，再升级提供者；本包不会透明提供旧能力。应替换原账号提供者，避免重复贡献账号入口。

## 构建与维护

需要 .NET 10、匹配的 MAUI 工作负载和 Python 3。在仓库根目录执行：

```powershell
git submodule update --init --recursive
pwsh .ci/Build.ps1
```

维护中的构建入口完成还原、格式验证、构建、测试并打包 `quantum.zju.plugin.cas-<version>-<platform>.zip`。Windows 和 Mac 分别提供原生程序集，发布包复用宿主公共 MAUI 框架。`.ci/Verify-Host.ps1` 使用固定提交的真实宿主验证发现、账号贡献、能力调用与生命周期。测试使用虚构数据，不连接学校。macOS 仍需独立构建与原生验证。

遵循[开发规范](../../.standards/docs/zh-CN/README.md)。真实凭据、Cookie、一次性测试程序及验收记录不进入 Git。

## 仓库分支流程

负责人：XmmShp。main 变更必须通过 PR，管理员也适用；不强制独立审批、CI 状态通过或讨论解决作为平台合并门槛。提交者仍需运行相关检查，并在 PR 中如实说明无法执行的验证。保留 Windows 和 macOS CI 配置。
