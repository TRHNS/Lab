# Threat model SecureLab, ЛР 6

Рівень: добрий.

## 1. Версія та середовище

| Параметр | Значення |
|---|---|
| review-start commit | a2441db944a7f5fb77f6f9bcf1e2109669dc28d0 ("Update SDK policy", від тегу v0.5.0) |
| гілка | lab/6-hardening-review |
| SDK | 10.0.401; global.json: 10.0.100 / latestFeature / allowPrerelease false |
| профілі | Development (профіль http, reset/seed дозволено, Database:AllowReset=true); production-like (`--no-launch-profile`, ASPNETCORE_ENVIRONMENT=Production, AllowReset=false) |
| дані | лише штучні seed-акаунти (alice, bob — Reporter; morgan — Analyst; admin — Administrator); пароль seed — у змінній SeedUsers__Password поза Git |
| транспорт | локальний HTTP http://localhost:5080 (production host і реальний TLS відсутні) |

## 2. Компоненти в межах огляду

- browser client — статичні сторінки з `src/SecureLab.Api/Client` (index.html, secure-app.html, auth-lab.html, browser-lab.html та їхні JS);
- ASP.NET Core API `SecureLab.Api` (minimal API, Identity + cookie, ProblemDetails, CSP middleware, rate limiter `login`);
- PostgreSQL у Docker Compose (`infra/compose.yaml`, порт 54329);
- конфігурація: `appsettings.json`, `appsettings.Development.json`, `infra/.env.example`, environment variables, `launchSettings.json`;
- журнали застосунку (console logging);
- NuGet-залежності та lockfiles (`packages.lock.json`).

Redis не використовується й до меж не входить.

## 3. Активи

| Актив | Чому цінний / уточнення |
|---|---|
| credentials seed-користувачів | у БД зберігається лише password hash (Identity); password hash ≠ password |
| authentication cookie | браузер додає його автоматично → дія від імені користувача |
| власні Incident (title, description, occurredAtUtc) | Reporter має право змінювати/видаляти лише свої |
| перехід статусу/severity (`POST /api/incidents/{id}/triage`) | привілейована дія ролі Analyst, лише New → Triaged |
| internal comments (IncidentComment) | не мають бути видимі Reporter, навіть власнику |
| IncidentStatusHistory / audit-записи логу | доказ, хто й що змінив |
| конфігураційні секрети (connection string, SeedUsers__Password) | доступ до БД і акаунтів |

## 4. Суб'єкти

| Суб'єкт | Можливості (з docs/access-matrix.md) |
|---|---|
| anonymous visitor | /health, статичні сторінки, login, register; решта → 401 |
| Reporter (alice, bob) | own list/details/update/delete (delete лише New), create for self |
| Analyst (morgan) | read all, triage New → Triaged; update/delete → 403 |
| Administrator | own як Reporter; triage → 403 |
| local developer | доступ до репозиторію, конфігурації, reset у Development |
| сторонній сайт у браузері жертви | може ініціювати cross-site запит, у який браузер додає cookie (CSRF) |

## 5. Entry points

### State-changing (периметр antiforgery, деталі — security-review.md, розд. 8)

| Method | Route | Auth | Джерело |
|---|---|---|---|
| POST | /api/auth/register | anonymous | Lab03Identity.cs |
| POST | /api/auth/login | anonymous, rate limit `login` | Lab03Identity.cs |
| POST | /api/auth/logout | cookie + antiforgery | Lab03Identity.cs |
| POST | /api/incidents | cookie + antiforgery | Lab02Endpoints.cs |
| PUT | /api/incidents/{id} | cookie, owner + antiforgery | Lab02Endpoints.cs |
| DELETE | /api/incidents/{id} | cookie, owner, статус New + antiforgery | Lab02Endpoints.cs |
| POST | /api/incidents/{id}/triage | cookie, Analyst + antiforgery | Lab02Endpoints.cs |

### Read-only

GET /health; GET /api/me; GET /api/auth/session-check; GET /api/incidents; GET /api/incidents/{id};
GET /api/incidents/search?q=&sortBy= (без автентифікації, rate limit `search`); GET /api/auth/csrf-token; GET /api/incidents/severity-summary; GET /api/incident-comments/{id};
у Development також /openapi і /scalar.

### Нe-HTTP entry points

configuration loading (appsettings*, env), `dotnet restore` (NuGet), запис логів, аргумент `--reset-database`.

## 6. Схема меж довіри

```
browser client (untrusted input, automatic auth cookie)
       │ HTTP, localhost:5080 (local development transport)
       ▼
ASP.NET Core API (validation, authn/authz, rate limit, CSP/headers, antiforgery, logging/audit)
       │ connection string / EF Core (Npgsql)
       ▼
PostgreSQL у Docker Compose (application data, Identity)

repository + NuGet sources ──► build/restore (--locked-mode)
environment configuration  ──► API startup (Development / Production)
```

## 7. Межі довіри, властивості та перевірки

| Межа | Очікувана властивість | Наявна перевірка |
|---|---|---|
| browser→API | cookie-authenticated POST/PUT/DELETE без валідного antiforgery request token відхилено, стан не змінено | REG-CSRF-01: `CsrfRegressionTests` |
| browser→API | текст користувача (description, comment) не виконується як код | REG-XSS-01 (ЛР 5, docs/browser-security-evidence.md); тест `ClientScript_DoesNotUseDangerousInnerHtmlSink` |
| browser→API | API не довіряє OwnerUserId з body; власник — з principal | `A04_CreateWithoutOwner_OwnerFromPrincipal`, `A05_CreateWithForeignOwner_OwnerStaysPrincipal` |
| browser→API | чужий Incident недоступний (404), Reporter не робить triage (403) | `A03_A10_AliceReadsBobIncident_Returns404`, `A04_AliceCannotUpdateBobIncident`, `A06_ReporterCannotTriage`, `A12_AliceCannotReadBobOrInternalComment` |
| browser→API | анонім отримує 401 без redirect; після logout сесія недійсна | `A01_AnonymousCreate_Returns401NotRedirect`, `A06_Logout_ThenProtectedRequestReturns401` |
| browser→API | перебір паролів і пошуку обмежено | `RateLimit_Login_Returns429ThenRecoversAfterWindow`, `Search_QuotaPerUser_Returns429_OtherUserNotBlocked_ThenRecovers` |
| browser→API | помилка 500 зберігає заголовки безпеки й не розкриває деталей | F-01: `UnexpectedError_KeepsSecurityHeaders` |
| API→PostgreSQL | параметр пошуку q/sortBy не змінює структуру SQL | S-02: `S02_SqlPayload_IsTreatedAsText_AndSortByIsAllowlisted` |
| repository→build | tracked files без реальних credentials; restore не змінює lockfile непомітно | git grep (етап 1 п. 3); `dotnet restore SecureLab.sln --locked-mode` |
| environment→configuration | у Production не діє Development-конфігурація, reset заборонено | Hosting environment: Production + відмова `--reset-database` (етап 1 п. 4) |

## 8. Наявні controls

| Control | Де |
|---|---|
| validation + ProblemDetails | Lab02Endpoints, `AddProblemDetails`, `UseExceptionHandler` |
| параметризовані запити EF Core | IncidentQueries, Lab02Endpoints (search) |
| Identity + cookie authentication | Lab03Identity.cs |
| ресурсна авторизація (owner/role) | IncidentQueries, Lab02Endpoints, docs/access-matrix.md |
| rate limiting `login` (10 / 30 с, за IP) | Program.cs |
| CSP, X-Content-Type-Options, Referrer-Policy, frame-ancestors 'none' | middleware у Program.cs |
| CORS лише в Development, лише origin 4173, GET | Program.cs (`Lab05BrowserProbe`) |
| antiforgery (header X-CSRF-TOKEN, explicit `AntiforgeryFilter`) | додано в ЛР 6 на POST/PUT/DELETE incidents, triage, logout |
| rate limiting `search` (5 / 10 с за id користувача) | додано в ЛР 6 |
| audit trail triage (`incident_status_history` + audit-подія) | додано в ЛР 6 (F-02) |

## 9. Виключення

| Не перевірялося | Причина |
|---|---|
| зовнішній penetration test, реальні системи | заборонено умовами роботи; лише локальний стенд |
| production host, реальний TLS/HSTS, reverse proxy | відсутні; транспорт — локальний HTTP |
| Docker, ОС, мережа хоста | поза межами застосунку |
| навантажувальне тестування / DoS | поза межами (окрім перевірки rate limiter) |
| Redis | не використовується |
| /openapi, /scalar | доступні лише в Development, у production-like не мапляться |
