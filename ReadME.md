# EmployeeSecureAPI – End‑to‑End Auth with Microsoft Entra ID (Azure AD)

This guide shows how to **protect a .NET API with Entra ID** and test it via:

* **Swagger UI (Authorization Code + PKCE)** — user‑interactive, no client secret.
* **Postman (Client Credentials)** — machine‑to‑machine using a confidential client and client secret.

It includes Azure App Registration steps, app configuration, run & test instructions, and troubleshooting.

---

## Prerequisites

* **.NET SDK** (8 or 9)
* An **Entra ID (Azure AD) tenant** with permissions to create app registrations
* Your **Tenant ID** and **Primary domain** (e.g., `contoso.onmicrosoft.com`)
* Optional: **Admin consent** privileges

---

## Repo layout (example)

```
EmployeeSecureAPI/
├── Controllers/
│   └── EmployeeController.cs
├── appsettings.json
├── Program.cs
└── EmployeeSecureAPI.csproj
```

---

## Overview: two flows, three app registrations

To support both user and machine access, you will use **three** app registrations in Entra ID:

1. **API (protected resource)** – e.g., `EmployeeSecureAPI`

   * Exposes a **delegated scope** for user‑based access: `Employee.Read`
   * Defines an **application role** for app‑only access: `Employee.Read.All`
   * Application (client) ID: **your API’s GUID** (you already have this)
   * Application ID URI (audience): `api://<API-CLIENT-ID>`

2. **Swagger SPA client** – e.g., `EmployeeSwaggerClient`

   * Platform: **Single‑page application (SPA)**
   * Redirect URI: `http://localhost:<PORT>/swagger/oauth2-redirect.html`
   * **Delegated permission** to API: `Employee.Read`
   * No client secret

3. **Machine client (confidential)** – e.g., `EmployeeTestClient`

   * Platform: **Web** (or Public client not recommended here)
   * Has a **Client secret** (or certificate)
   * **Application permission** to API: `Employee.Read.All` (admin consent)

> Your API validates **Bearer JWTs** from both flows. Swagger uses the user‑delegated scope `Employee.Read`. Postman (client credentials) uses the app role `Employee.Read.All` and requests `scope=api://<API-CLIENT-ID>/.default`.

---

## A. Azure App Registration — API (EmployeeSecureAPI)

1. **Create / open** app registration for your API.
2. **Expose an API**

   * Set **Application ID URI** to: `api://<API-CLIENT-ID>`
     (Example: `api://5c17aa47-dd5d-4171-97f0-860554a448b1`)
3. **Add a delegated scope**

   * Click **Add a scope** →

     * Scope name: `Employee.Read`
     * Who can consent: Admins and users
     * Admin consent display name: `Read employee data`
     * User consent display name: `Read employee data`
     * State: Enabled
4. **Add an application role (for client credentials)**

   * Go to **App roles** → **Create app role**

     * Display name: `Employee.Read.All`
     * Allowed member types: **Applications**
     * Value: `Employee.Read.All`
     * Description: `Read employee data (application-only)`
     * Do **not** select Users/Groups for this one (it’s for app-only)
   * Save

> Why both? **Delegated scopes** (`scp` claim) are for user flows. **Application roles** (`roles` claim) are for app-only flows.

---

## B. Azure App Registration — Swagger SPA client (EmployeeSwaggerClient)

1. **Register** a new app (or use your existing one): `EmployeeSwaggerClient`.
2. **Authentication**

   * Platform: **Single-page application (SPA)**
   * Redirect URIs (add both for safety):

     * `http://localhost:<PORT>/swagger/oauth2-redirect.html`
     * `https://localhost:<PORT>/swagger/oauth2-redirect.html`
   * (No client secret for SPA)
3. **API permissions**

   * **Add a permission** → **My APIs** → select `EmployeeSecureAPI` → **Delegated permissions** → check `Employee.Read`
   * **Grant admin consent** (recommended to avoid interactive consent prompts)

> Common Swagger errors and fixes are listed in **Troubleshooting** below.

---

## C. Azure App Registration — Machine client (EmployeeTestClient)

1. **Register** a new app: `EmployeeTestClient` (this is your Postman/daemon client).
2. **Certificates & secrets**

   * Create a **Client secret** and copy its value.
3. **API permissions**

   * **Add a permission** → **My APIs** → select `EmployeeSecureAPI` → **Application permissions** → select `Employee.Read.All`
   * **Grant admin consent**

> Client credentials **requires** an application permission (app role) on the API. You cannot use delegated scopes in client credentials.

---

## Application configuration

### `appsettings.json` (example)

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<YOUR_TENANT_ID>",
    "Domain": "<yourtenant>.onmicrosoft.com",
    "ClientId": "<API_CLIENT_ID>",
    "Audience": "api://<API_CLIENT_ID>"
  },
  "SwaggerOAuth": {
    "ClientId": "<SWAGGER_SPA_CLIENT_ID>",
    "UsePkce": true,
    "Scopes": "api://<API_CLIENT_ID>/Employee.Read"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### `Program.cs` (key points)

* `AddMicrosoftIdentityWebApi` with `AzureAd` section (validates JWT)
* Swagger `AuthorizationCode` flow with **PKCE** and the delegated scope
* UI config uses SPA client id and redirect URL

> You already have a working `Program.cs` from our session. Ensure `o.OAuthUsePkce();`, `o.OAuthClientId(<SPA_CLIENT_ID>)`, and the scope string matches the delegated scope you created.

---

## Run locally

1. Trust dev cert (first time only):

   ```bash
   dotnet dev-certs https --trust
   ```
2. Run the API:

   ```bash
   dotnet run
   ```
3. Open Swagger UI:
   `https://localhost:<PORT>/swagger`

---

## Test via Swagger (Authorization Code + PKCE)

1. Click **Authorize** in Swagger UI.
2. Ensure the dialog shows:

   * Authorization URL / Token URL for your **tenant**
   * **Client ID** = SPA client id (`EmployeeSwaggerClient`)
   * **Scope** = `api://<API_CLIENT_ID>/Employee.Read`
3. Check the scope → **Authorize** → sign in → **Authorize**
4. Try a protected endpoint (e.g., `GET /api/employee`) → should succeed.
5. (Optional) Inspect token:

   * Open browser **DevTools → Network** → select the API request → **Request Headers** → copy `Authorization: Bearer <token>`
   * Decode at **[https://jwt.ms](https://jwt.ms)** → verify claims: `aud` (your API), `scp` includes `Employee.Read`.

---

## Test via Postman (Client Credentials)

> This uses the **Machine client** (`EmployeeTestClient`) with a **client secret** and the API’s **application permission** (`Employee.Read.All`).

### 1) Get a token

**Request**

* Method: `POST`
* URL: `https://login.microsoftonline.com/<TENANT_ID>/oauth2/v2.0/token`
* Headers: `Content-Type: application/x-www-form-urlencoded`
* Body (x-www-form-urlencoded):

  * `client_id`: `<MACHINE_CLIENT_ID>`
  * `client_secret`: `<MACHINE_CLIENT_SECRET>`
  * `grant_type`: `client_credentials`
  * `scope`: `api://<API_CLIENT_ID>/.default`

**Success response** will include `access_token`.

> The `.default` scope instructs Entra ID to issue the token with all **application permissions** that have admin consent (here, `roles: ["Employee.Read.All"]`).

### 2) Call your API

* Method: `GET`
* URL: `https://localhost:<PORT>/api/employee`
* Header: `Authorization: Bearer <access_token>`

If your endpoints only require `[Authorize]`, the call should succeed. If you enforce role/scope checks, see the next section.

---

## (Optional) Enforcing scopes/roles in the API

### Enforce delegated scope (Swagger/user flow) on a controller

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web.Resource;

[ApiController]
[Route("api/[controller]")]
public class EmployeeController : ControllerBase
{
    private static readonly string[] scopeRequiredByApi = new[] { "Employee.Read" };

    [HttpGet]
    [Authorize]
    public IActionResult Get()
    {
        HttpContext.VerifyUserHasAnyAcceptedScope(scopeRequiredByApi);
        // ... return employees
        return Ok(new [] { new { Id = 1, Name = "Abhishek", Role = "Engineer" } });
    }
}
```

### Enforce application role (Postman/m2m flow)

```csharp
[Authorize(Roles = "Employee.Read.All")]
[HttpGet("admin")] // e.g., an app-only endpoint
public IActionResult GetAdmin()
{
    return Ok(new { Secret = "app-only data" });
}
```

> In client credentials, the token won’t have `scp`; it will have `roles`.

---

## Troubleshooting (common Azure errors)

### AADSTS50011 — Redirect URI mismatch

* *Message:* The redirect URI in the request does not match the app’s configured redirect URIs.
* **Fix:** In **Swagger SPA client** → **Authentication** → add `http(s)://localhost:<PORT>/swagger/oauth2-redirect.html`.

### AADSTS9002326 — Cross‑origin token redemption allowed only for SPA

* *Cause:* Swagger client registered as **Web** app instead of **SPA**.
* **Fix:** Change platform to **Single‑page application**. Remove client secret for Swagger app.

### AADSTS501481 — Code\_Verifier does not match code\_challenge

* *Cause:* PKCE misconfigured or client secret being used with SPA.
* **Fix:** Ensure `OAuthUsePkce()` in Swagger UI, **do not** supply `client_secret` for SPA, avoid multiple conflicting Swagger tabs.

### AADSTS7000216 — invalid\_client (client\_credentials requires secret)

* *Cause:* Missing `client_secret` (or certificate) for machine client.
* **Fix:** Create a client secret in **EmployeeTestClient** (machine client). Use `scope=api://<API_CLIENT_ID>/.default`. Ensure **Application permission** `Employee.Read.All` is granted with admin consent.

### 401 Unauthorized

* *Causes:* No token, expired token, wrong audience (`aud`), missing scope/role, wrong tenant.
* **Checklist:**

  * `aud` must equal `api://<API_CLIENT_ID>`
  * User flow token has `scp` with `Employee.Read`
  * App flow token has `roles` with `Employee.Read.All`
  * `iss` matches your tenant

---

## Production notes

* Add your deployed URL as a **redirect URI** for the Swagger SPA client (e.g., `https://api.example.com/swagger/oauth2-redirect.html`).
* Use **Key Vault** for secrets; rotate regularly.
* Lock down Swagger in prod (e.g., behind auth, or disable Try‑it‑out).
* Consider **CORS** if you add a separate frontend.
* Use **policies** to require scopes/roles per endpoint.

---

## Appendix: Example App Role JSON (manifest view)

If you prefer editing the API app manifest directly, add an app role like below:

```json
"appRoles": [
  {
    "allowedMemberTypes": [ "Application" ],
    "description": "Read employee data (application-only)",
    "displayName": "Employee.Read.All",
    "id": "<new-guid-here>",
    "isEnabled": true,
    "value": "Employee.Read.All"
  }
]
```

Generate a new GUID for `id`.

---

## Appendix: Quick Postman OAuth 2.0 setup (UI)

* **Type:** OAuth 2.0 → **Get New Access Token**
* **Token Name:** `EmployeeAPI-CC`
* **Grant Type:** Client Credentials
* **Access Token URL:** `https://login.microsoftonline.com/<TENANT_ID>/oauth2/v2.0/token`
* **Client ID:** `<MACHINE_CLIENT_ID>`
* **Client Secret:** `<MACHINE_CLIENT_SECRET>`
* **Scope:** `api://<API_CLIENT_ID>/.default`
* **Client Authentication:** Send as **Body**
* Click **Get New Access Token** → **Use Token**

---

## You’re done 🎉

You now have a .NET API protected by Entra ID that supports **both user login (PKCE)** via Swagger and **machine-to-machine (client credentials)** via Postman. If something’s off, jump to **Troubleshooting** and verify your app registrations and claims in the token.
