# 浙江大学身份插件
[English](../en/README.md) · [术语表](glossary.md)

插件 ID 为 `quantum.zju.plugin.oidc`，使用已发布的 [SDK](glossary.md#sdk) 0.3.0-rc.3，向 `quantum.settings/accounts` 贡献浙江大学账号区域。宿主必须实现该扩展点；集成测试通过 `.ci/desktop-revision.txt` 固定公开宿主提交，不依赖相邻源码目录。

## 登录

打开**设置 → 账号 → 浙江大学账号**，输入旧教务接口使用的账号标识，选择**打开浙大登录页**。原生 [WebView](glossary.md#webview) 打开学校教务登录入口，由学校页面完成密码加密、跳转和额外验证。登录后选择**使用此登录会话**。插件表单不收集密码。目前未启用第三方身份提供方的跨域跳转，请使用学校账号或学校域名内的验证流程。

适配器通过原生接口读取包括 [HttpOnly](glossary.md#httponly) 在内的 Cookie。Windows 使用 WebView2，Mac Catalyst 使用 WKWebView。读取覆盖 `/cas/`、`/jwglxt/`、`/api/` 路径；插件会独立验证教务 JSON 只读请求后再接受新会话。替换失败或取消会保留旧会话，不能仅凭 HTTP 200 判断登录成功。

当前学校入口实际使用 [CAS](glossary.md#cas)。插件名称含 [OIDC](glossary.md#oidc) 并不表示本实现把 CAS 票据当成 OIDC 令牌，也未虚构学校未提供的 OIDC 客户端配置。

原生适配器是与便携插件入口并列发布的平台程序集，复用宿主的公共 MAUI 框架，不复制宿主源码、不访问宿主内部实现、不改 SDK。不同系统分别打包；macOS 需要独立构建和原生验证。

## 会话边界

Cookie 只保留在本地受信任的 .NET 服务中，不进入扩展 JSON、RPC 响应、日志或发布产物。学校浏览器存储可能保留 SSO 会话。**断开本地连接**只释放插件 HTTP 会话，不执行学校全局退出，也不清理宿主其它浏览器数据。账号标识是用户提供给旧接口的路由信息，并非独立的身份声明。

通过 [RPC](glossary.md#rpc) 暴露的 `zjuam.read-resource` 仅支持 `xzzd-todos`、`zdbk-courses`、`zdbk-sections`、`zdbk-selected`、`zdbk-graduation`、`zdbk-introduction`。保留现有请求、响应命名空间以兼容 Zdbk/Xzzd。该能力只允许一个提供者，应替换旧 `quantum.plugin.zjuam`，不能同时启用两个插件。

## 构建与维护

需要 .NET 10、匹配的 MAUI 工作负载及 Python 3。Windows 和 Mac 分别构建对应原生目标，在仓库根目录执行：

```powershell
git submodule update --init --recursive
pwsh .ci/Build.ps1
```

维护中的构建入口完成还原、格式验证、构建、测试和打包；原生适配器不重复打包由宿主提供的框架程序集。`.ci/Verify-Host.ps1` 使用固定版本真实宿主验证发现、账号贡献和 JSON 调用。测试使用虚构数据，不连接学校。真实账号文件、Cookie、个人记录、一次性验证脚本和结果必须保留在 Git 之外。遵循[开发规范](../../.standards/docs/zh-CN/README.md)。

## 仓库分支流程

负责人：XmmShp。所有 main 变更必须通过 PR，管理员也适用；不强制独立审批、CI 状态通过或讨论解决作为平台合并门槛。提交者仍应运行相关验证，并在 PR 中如实说明失败或无法执行的检查。保留 Windows 和 macOS CI 配置。
