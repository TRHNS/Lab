# Security review SecureLab

Рівень: добрий. Baseline: 2-A (ресурс Incident). Гілка `lab/6-hardening-review`.

## 1. Executive summary

Перевірено SecureLab від review-start commit `a2441db` до `60beb44` у локальному стенді
(ASP.NET Core API + browser client + PostgreSQL у Docker Compose) у профілях Development і
production-like; лише штучні акаунти. У межах огляду – браузерні заголовки, CORS, cookie,
CSRF-периметр змінювальних маршрутів, безпечні помилки, секрети, залежності, журнали й
rate limiting; поза межами – production host, реальний TLS/HSTS, зовнішні системи.
Зафіксовано дві findings (F-01 і F-02, обидві P2), обидві виправлено й повторно перевірено.
Додано antiforgery на весь cookie-периметр і rate limit на пошук. Final regression (33
автоматизовані тести + ручні REG-CORS-01 і REG-XSS-01) пройшов після reset БД.
Найважливіші залишкові ризики: анонімний доступ до пошуку, login/register CSRF і відсутність
перевірки транспорту в production.

## 2. Scope та exclusions

Межі огляду, активи, суб'єкти, entry points і виключення – у `docs/threat-model.md` (розд. 1–5, 9).
Коротко: локальний стенд, лише штучні акаунти alice, bob (Reporter), morgan (Analyst),
admin (Administrator). Не перевірялись: зовнішній penetration test, production host і reverse
proxy, реальний TLS/HSTS, Redis (не використовується), навантажувальне тестування.

## 3. System overview і trust boundaries

Схема меж довіри й таблиця «межа → властивість → перевірка» – `docs/threat-model.md`
(розд. 6–7). Межі: browser→API, API→PostgreSQL, repository→build, environment→configuration.

## 4. Methodology та test environment

| Параметр | Значення |
|---|---|
| Review-start commit | a2441db944a7f5fb77f6f9bcf1e2109669dc28d0 |
| Коміти етапів | 13fe617 (етап 2), 60beb44 (етап 3) |
| SDK / OS | .NET SDK 10.0.401; Windows, Git Bash |
| Профілі | Development (`dotnet run --project src/SecureLab.Api`); production-like (`ASPNETCORE_ENVIRONMENT=Production`, `--no-launch-profile`, `-c Release`, `ConnectionStrings__SecureLab` з env) |
| БД | PostgreSQL 18.4 у Docker Compose, 127.0.0.1:54329; reset `-- --reset-database` лише в Development |
| Клієнти | curl, Microsoft Edge (DevTools Network/Console), xUnit + WebApplicationFactory |
| Пароль seed | змінна `SeedUsers__Password` поза Git |

Метод: спостереження фактичних відповідей, заголовків і журналів → порівняння з очікуваною
властивістю → finding або перевірений control → регресійний тест.

## 5. Findings summary

| ID | Finding | Asset | Impact | Likelihood | Priority | Status | Retest / регресія |
|---|---|---|---|---|---|---|---|
| F-01 | 500 без заголовків безпеки | захисні заголовки браузера | Low | Medium | P2 | Fixed (`13fe617`) | UnexpectedError_KeepsSecurityHeaders |
| F-02 | дозволений triage без audit trail | відстежуваність зміни Incident.Status | Medium | High | P2 | Fixed (`60beb44`) | REG-AUDIT-01 |

Порядок виправлення: F-01 виправлено першою, бо вона виявлена першою на етапі 2 і стосується
спільного для всіх відповідей middleware (мала зміна, широке покриття). F-02 має вищий impact
для підзвітності, але не відкриває даних і не дозволяє несанкціонованої дії, тому виправлена
на етапі 3 разом з оглядом журналів. Тимчасово прийнятих findings немає.

## 6. Detailed findings

- `docs/findings/F-01-error-headers.md`
- `docs/findings/F-02-triage-audit.md`

## 7. Regression results

Фінальний прогін на `60beb44` після reset БД: `dotnet test SecureLab.sln -c Release` →
total 33, failed 0.

| ID сценарію | Property | Передумови | Негативний сценарій | Позитивна перевірка | Evidence / command | Result |
|---|---|---|---|---|---|---|
| T-02, T-03 (ЛР 2) | server validation | reset + login alice + токен | невалідний severity → 400 з errors.severity; дубль title → 409 | перший POST → 201 | T02_ValidationFails_Returns400, T03_DuplicateTitle_Returns409 | pass |
| S-02 (ЛР 2) | query не змінює структуру SQL | seed | `' OR '1'='1` → 0 рядків; sortBy з SQL → 400 | порожній q → seed-інциденти | S02_SqlPayload_IsTreatedAsText_AndSortByIsAllowlisted | pass |
| A-01, A-02, A-06, A-07 (ЛР 3) | identity lifecycle | alice, bob | анонім → 401; після logout → 401; невідомий логін = невірний пароль | login → /api/me = alice; logout → 204 | SecurityScenarioTests | pass |
| A-03/A-10, A-04, A-06, A-12 (ЛР 4) | ownership/role | Alice/Bob/Morgan seed | чужий інцидент → 404; Reporter triage → 403; internal comment → 404 | власник читає; Analyst triage → 200 | AuthorizationTests | pass |
| REG-XSS-01 (ЛР 5) | text не виконується як code | marker у seed comment | marker не виконується | текст відображається | ClientScript_DoesNotUseDangerousInnerHtmlSink; процедура ЛР 5 | pass |
| REG-CORS-01 (ЛР 5) | exact browser origin scope | 4173 / 4174, Development | 4174 – браузер блокує читання | 4173 читає /api/me | ручна процедура (звіт, рис. 16–17) | pass |
| REG-CSRF-01 (ЛР 6) | token pair required | cookie alice + reset | без / невалідний токен → 400, інцидент не створено | валідна пара → 201, інцидент у списку | CsrfRegressionTests | pass |
| RateLimit login (ЛР 3), search (ЛР 6) | abuse flow limited | 10/30 с; 5/10 с | 11-та спроба → 429; 6-й пошук → 429 | інший користувач → 200; після вікна → 200 | RateLimit_Login_…, Search_QuotaPerUser_… | pass |
| F-01 (ЛР 6) | заголовки на помилці | Production, недосяжна БД | — | 500 з nosniff, Referrer-Policy, CSP | UnexpectedError_KeepsSecurityHeaders | pass |
| REG-AUDIT-01 (ЛР 6) | audit trail | alice, morgan, новий інцидент | Reporter triage → 403, без запису | Analyst → запис в історії | AuditTrailTests | pass |

## 8. Hardening controls

### Периметр змінювальних маршрутів

Antiforgery header: `X-CSRF-TOKEN`; cookie: `securelab-af`; видача токена:
`GET /api/auth/csrf-token` (лише автентифікованим); валідація: `AntiforgeryFilter`
(`src/SecureLab.Api/Security/AntiforgeryFilter.cs`) → 400 Problem Details.

| ID | Method + route | Джерело / розбіжність | Зміна стану | Cookie authentication | Browser-accessible | Antiforgery validation | Regression ID |
|---|---|---|---|---|---|---|---|
| SC-01 | POST /api/incidents | ЛР 2, server + client | створює інцидент | так | так | explicit: AntiforgeryFilter | REG-CSRF-01 |
| SC-02 | PUT /api/incidents/{id} | ЛР 4, DISCREPANCY закрито: клієнт не викликає (.http), внесено в периметр | змінює title/description | так | так | explicit: AntiforgeryFilter | Perimeter_WithoutToken |
| SC-03 | DELETE /api/incidents/{id} | ЛР 4, DISCREPANCY закрито | видаляє інцидент | так | так | explicit: AntiforgeryFilter | Perimeter_WithoutToken |
| SC-04 | POST /api/incidents/{id}/triage | ЛР 4, DISCREPANCY закрито | змінює status | так | так | explicit: AntiforgeryFilter | Perimeter_WithoutToken |
| SC-05 | POST /api/auth/logout | ЛР 3, server + client | завершує сеанс | так | так | explicit: AntiforgeryFilter, до SignOutAsync | Perimeter_WithoutToken |
| SC-06 | POST /api/auth/login | ЛР 3, server + client | створює сеанс | ні | так | не застосовується: login CSRF – residual risk | — |
| SC-07 | POST /api/auth/register | ЛР 3, server + client | створює акаунт | ні | так | не застосовується: account-creation CSRF – residual risk | — |

### Control → risk → verification → boundary

| Control | Ризик | Verification | Межа |
|---|---|---|---|
| server-side authorization (owner/role) | IDOR/BOLA, підвищення привілеїв | A-03/A-10, A-04, A-06 | не замінює authentication; пошук без входу |
| antiforgery validation (explicit filter) | browser CSRF з автоматичною cookie | REG-CSRF-01, Perimeter_WithoutToken | не зупиняє XSS на власному origin; login/register поза периметром |
| safe errors + correlation id | розкриття деталей у відповіді | контрольована 500, traceId у відповіді й журналі | не замінює контроль доступу до журналів |
| CSP + nosniff + Referrer-Policy на всіх відповідях | browser defense in depth | заголовки 5 класів відповідей, F-01 retest | не замінює safe DOM |
| CORS: один origin 4173, лише Development | читання відповідей стороннім origin | REG-CORS-01 | не є authorization і не зупиняє CSRF |
| cookie HttpOnly, SameSite=Lax, Secure поза Development | крадіжка cookie через JS, частина cross-site запитів | Set-Cookie, CookieFlags_LoginOverHttps_… | HttpOnly не робить XSS неможливим |
| rate limiting login (IP) і search (id користувача) | перебір паролів, зловживання пошуком | RateLimit_Login_…, Search_QuotaPerUser_… | не замінює authentication; in-memory, один екземпляр |
| audit trail triage | неможливість розслідувати привілейовані зміни | REG-AUDIT-01 | console sink без захисту від зміни |
| параметризовані запити + allowlist sortBy | SQL injection | S-02 | лише перевірені маршрути пошуку |

### Транспорт

Лише профіль `http` (http://localhost:5080); `UseHttpsRedirection()` і `UseHsts()` відсутні;
reverse proxy немає. HTTPS, redirect і HSTS у локальному стенді не налаштовано й не
перевірялись; відсутність HSTS на loopback не є дефектом (ExcludedHosts).

## 9. Dependency and secret audit

**Залежності.** 10.11.2026, SDK 10.0.401: `dotnet restore SecureLab.sln` → код 0, без NU1901–NU1904;
`dotnet package list --vulnerable --include-transitive` → код 0, для SecureLab.Api і
SecureLab.Api.Tests advisories не знайдено (api.nuget.org). Dependency decision table порожня:
update / mitigate / temporary acceptance, NuGetAuditSuppress і WarningsNotAsErrors не
застосовувались. Межа: лише відомі на цю дату advisories одного джерела; досяжність коду не оцінювалась.

**Секрети.** Конфігурація (етап 1): 4 tracked config-файли, `.env` і `appsettings.Local.json`
ігноруються; історія конфігурації – лише `57bef3b`. Систематичний пошук (етап 3): 81 tracked
file на `13fe617`, широкий шаблон – 220 збігів, точний пошук значень – 6 рядків. Єдине
значення – відкритий навчальний пароль локальної БД `local-study-password` (accepted,
study-only, README:63); решта – назви полів, env-змінні, placeholders. Реальних credentials
не знайдено. Межа: поточні tracked files; stash, інші клони й CI не перевірено.

**Журнали.** Діагностичні записи login без логіна й пароля; audit-події `authorization.denied`
і `incident.status_changed` містять user id, дію, інцидент, результат, час і trace, без cookie,
токенів і тіла запиту; structured logging, недовірений текст у журнал не потрапляє.

## 10. Residual risks

| Ризик | Чому прийнято | Compensating control / follow-up |
|---|---|---|
| `GET /api/incidents/search` доступний без входу й повертає інциденти всіх користувачів | у матриці доступу ЛР 4 віднесено до рівня «Відмінно» | rate limit за IP; follow-up – вимагати authorization і фільтр за власником |
| login / register CSRF | захист потребує окремого анонімного bootstrap токена | SameSite=Lax; login rate limit |
| транспорт у production (TLS, HSTS) не перевірено | production host відсутній | cookie SecurePolicy.Always у Production |
| Development CORS-origin 4173 може прочитати antiforgery-токен | probe ЛР 5 – довірений локальний origin | policy реєструється лише в Development |
| in-memory rate limiter | один екземпляр API | для кількох екземплярів – спільний лічильник |
| audit-події лише в console sink | навчальний стенд | стійкий запис triage в `incident_status_history` |

## 11. Conclusion

Обидві findings виправлено й покрито регресійними тестами; cookie-периметр змінювальних
маршрутів повністю захищено antiforgery; final regression рівня «Добре» пройшов. Висновок
стосується лише локального стенда, перевірених маршрутів і сценаріїв.

## 12. Evidence appendix

| ID | Що доводить | Де лежить / як повторити | Результат |
|---|---|---|---|
| EV-01 | 500 без заголовків до виправлення | Production + БД 54399, `POST /api/auth/login` (звіт, рис. 10) | заголовки відсутні |
| EV-02 | F-01 retest | той самий запит на `13fe617` (рис. 15); тест UnexpectedError_KeepsSecurityHeaders | заголовки є; Passed |
| EV-03 | REG-CORS-01 | probes 4173/4174, Development (рис. 16–17) | 4173 читає, 4174 заблоковано |
| EV-04 | POST без токена не змінює стан | curl з cookie alice без header (рис. 23); CsrfRegressionTests | 400, інцидент не створено; Passed |
| EV-05 | браузерний flow з токеном | Network: header X-Csrf-Token, 201 (рис. 25–26, значення приховано) | 201 |
| EV-06 | rate limiting | `dotnet test --filter FullyQualifiedName~RateLimit` | Passed: 2 |
| EV-07 | audit trail | AuditTrailTests; рядки журналу (рис. 39) | Passed; denied / allowed записані |
| EV-08 | аудит залежностей | `dotnet package list --vulnerable --include-transitive` (рис. 34) | без advisories, код 0 |
| EV-09 | пошук секретів | `git grep` по 81 tracked file (рис. 35) | 220 / 6 збігів, credentials немає |
| EV-10 | final regression | reset + `dotnet test SecureLab.sln -c Release` на `60beb44` (рис. 41–42) | 33 / 0 failed |
