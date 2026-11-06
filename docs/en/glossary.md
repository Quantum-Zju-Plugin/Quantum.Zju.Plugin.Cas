# Glossary

[简体中文](../zh-CN/glossary.md)

<a id="sdk"></a>
## SDK — Software Development Kit

The published Quantum plugin contracts and extension protocol.

<a id="rpc"></a>
## RPC — Remote Procedure Call

Named JSON request/response capabilities dispatched through NOF.

<a id="webview"></a>
## WebView — Embedded browser

WebView2 on Windows and WKWebView on Mac Catalyst display the official university page.

<a id="httponly"></a>
## HttpOnly — Script-inaccessible cookie

A cookie unavailable to page JavaScript; native cookie APIs can retrieve it.

<a id="cas"></a>
## CAS — Central Authentication Service

An SSO protocol using service-bound tickets and application sessions.

<a id="oidc"></a>
## OIDC — OpenID Connect

An identity protocol over OAuth 2.0; distinct from the CAS route implemented here.

<a id="sso"></a>
## SSO — Single Sign-On

One identity-provider login can authenticate multiple registered services; each service still establishes its own session.

<a id="service-ticket"></a>
## ST — Service Ticket

A short-lived credential bound to one CAS service and valid for one validation attempt. A callback containing it is sensitive.

<a id="tgc"></a>
## TGC — Ticket-Granting Cookie

The browser cookie representing the CAS single-sign-on session. It remains private to the account provider.

<a id="user-agent"></a>
## User-Agent — Browser identity header

The HTTP client identifier sent by the browser. Ticket acquisition preserves it because the authentication service can bind the session to it.
