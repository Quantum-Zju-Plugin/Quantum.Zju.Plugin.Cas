# 外部集成合约

[English](../en/integration.md) · [文档入口](README.md) · [术语表](glossary.md)

## 账号元数据

通过宿主 [RPC](glossary.md#rpc) 目录发现 `zju.cas.account`，发送 `{}`。响应值包含：

```json
{"provider":"zju","protocol":"cas","isSignedIn":true,"sessionId":"opaque-local-connection-id"}
```

断开时 `isSignedIn` 为 false，`sessionId` 为 null。该标识会在重新连接时变化，不是学号、已验证的身份声明、浏览器 Cookie 或访问令牌。此调用只读取本地状态，不访问网络。交互登录仅由设置页账号入口发起，后台能力调用不会弹出登录窗口。

## 服务认证

使用当前 `sessionId` 和目标网站给出的精确 [CAS](glossary.md#cas) 服务标识调用 `zju.cas.service-ticket`：

```json
{"sessionId":"opaque-local-connection-id","serviceUrl":"https://service.zju.edu.cn/callback"}
```

示例域名代表假设的已注册服务，并非内置站点集成。响应值有四个字段：

```json
{"sessionId":"opaque-local-connection-id","serviceUrl":"https://service.zju.edu.cn/callback","callbackUrl":"https://service.zju.edu.cn/callback?ticket=ST-placeholder","userAgent":"Browser/placeholder"}
```

`callbackUrl` 携带[服务票据](glossary.md#service-ticket)，属于凭据，仅限对应服务的一次校验。禁止记录、持久化、缓存或重用。提供者会先验证回跳地址的协议、主机、路径、已有查询参数及票据结构，不会跟随此回跳，也不会交出自己的[票据授予 Cookie](glossary.md#tgc)。服务是否已注册且有权访问由学校决定。服务地址须使用 `zju.edu.cn` 下默认端口的 HTTPS，不能包含用户信息、片段或预置 `ticket` 参数；目前不支持仅注册了 HTTP 标识的旧服务。

消费方应当：

1. 持有独立的站点会话，与账号提供者的会话分开。
2. 将返回的 [User-Agent](glossary.md#user-agent) 用于认证请求。使用框架 Cookie 容器创建自己的客户端，不导出 CAS Cookie 容器。
3. 立即按照目标网站的登录流程消费回跳。有些站点直接接受回跳，有些要求在同站登录接口交换票据。不要先自行调用 CAS 票据校验接口，然后重复使用已被消费的票据。
4. 只跟随该集成批准的安全地址，并通过受保护的响应验证成功，不能只看 HTTP 200。得到的站点 Cookie 或令牌保留在该集成内部。
5. 在本仓库之外实现业务请求、数据结构、身份信息提取及解析。需要学校账号标识时，从站点认证后的个人资料获得，不得从 `sessionId` 推断。
6. 在本地断开或重连时按需清理自己的站点会话，建立新会话前重新读取账号元数据。提供者重新加载也会使旧连接标识失效。

两项能力均由唯一提供者按单提供者有序派发。外部插件在清单中声明实际使用的能力。本合约不包含通用认证 HTTP 转发、任意浏览器脚本执行或业务专属资源名称。

## 错误与生命周期

| 错误码 | 含义 |
| --- | --- |
| `zju_cas_sign_in_required` | 没有本地账号连接，请用户通过账号设置登录。 |
| `zju_cas_session_changed` | 连接标识过时，需要重新读取账号元数据。 |
| `zju_cas_invalid_service` | 服务标识不符合目标地址策略。 |
| `zju_cas_ticket_unavailable` | 未签发有效的回跳票据；可能是服务未获授权，或登录已经过期。 |
| `zju_cas_invalid_callback` | 回跳地址与请求服务不匹配，或票据结构不符合要求。 |
| `zju_cas_transport_failed` | 无法连接学校认证服务。 |

请求支持取消。不得自动反复提交密码、虚构学校客户端注册信息或关闭证书校验。某个服务请求失败不会清除仍可用的本地账号连接。设置页登录可以替换连接；替换失败时保留旧连接。本地断开不会吊销外部集成已经建立的站点会话，也不执行学校全局退出。

[CAS 协议规范](https://apereo.github.io/cas/development/protocol/CAS-Protocol-Specification.html)定义了服务绑定及单次使用的票据语义。本插件是该认证服务的客户端，不是新建的身份认证服务器。
