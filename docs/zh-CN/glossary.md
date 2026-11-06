# 术语表

[English](../en/glossary.md)

<a id="sdk"></a>
## SDK — 软件开发工具包

已发布的 Quantum 插件合约与扩展协议。

<a id="rpc"></a>
## RPC — 远程过程调用

由 NOF 分发的命名 JSON 请求与响应能力。

<a id="webview"></a>
## WebView — 内嵌浏览器

Windows 的 WebView2 与 Mac Catalyst 的 WKWebView 用于显示学校官方页面。

<a id="httponly"></a>
## HttpOnly — 脚本不可读 Cookie

页面脚本不能读取的 Cookie，可通过原生 Cookie 接口读取。

<a id="cas"></a>
## CAS — 集中认证服务

通过服务绑定票据和应用会话实现单点登录的协议。

<a id="oidc"></a>
## OIDC — 开放身份连接

基于 OAuth 2.0 的身份协议，与此处实际使用的 CAS 路线不同。

<a id="sso"></a>
## SSO — Single Sign-On（单点登录）

向身份提供方登录一次后，可认证多个已注册服务；各服务仍需建立自己的会话。

<a id="service-ticket"></a>
## ST — Service Ticket（服务票据）

绑定一个 CAS 服务、仅能校验一次的短期凭据。携带票据的回跳地址属于敏感信息。

<a id="tgc"></a>
## TGC — Ticket-Granting Cookie（票据授予 Cookie）

代表 CAS 单点登录会话的浏览器 Cookie，由账号提供者私有保存。

<a id="user-agent"></a>
## User-Agent — 浏览器身份请求头

浏览器发送的 HTTP 客户端标识。获取票据时保留它，因为认证服务可能将会话绑定到该标识。
