# Докази безпеки браузера (DEL-01)

Рівень: добрий «Добре». Рівні накопичувальні: виконано обов'язковий результат для всіх і доповнення рівня «Добре». Паролі, cookie/token values і export browser storage не наводяться.

## Стенд і Git-стан (Для всіх)

ОС: Windows, термінал Git Bash. Browser: Opera, версія 138.0.6044.45. .NET SDK: 10.0.303. API origin: http://localhost:5080. Probe origins: http://localhost:4173 — дозволений, http://localhost:4174 — контрольний недозволений.

Release: lab-05-start-v1, inherits: v0.4.0. base_commit: 967b2055748c1a0f2856fd928fd72b9db4a09580. entry_parent: 967b2055748c1a0f2856fd928fd72b9db4a09580. Гілка lab/5-browser-xss створена від тегу v0.4.0, рис. 1; commit політики SDK не робився, тому entry_parent збігається з base_commit.

Vulnerable commit: 5197a75 — встановлено scaffold lab-05-start-v1, уразливий стан, рис. 2. Fixed commit: буде заповнено після Етапу 2. Тег v0.5.0, main і спосіб передавання: буде заповнено після Етапу 4.

Запуск стенда: БД піднято, навчальні дані скинуто, рис. 3; тести 25 із 25 пройдено, API запущено, рис. 4.

Механізм сеансу: cookie .AspNetCore.Identity.Application; Domain: localhost; Path: /; Expires: Session; HttpOnly: так; SameSite: Lax; Secure: ні, бо локальний стенд працює по HTTP. Value не наводиться. Доказ: рис. 11. HttpOnly означає, що JavaScript на сторінці не може прочитати цей cookie через document.cookie.

## Гіпотеза й REG-XSS-01 (Для всіх)

Гіпотеза: значення з поля форми <textarea name="description"> на browser-lab.html надсилається як JSON-поле description у POST /api/incidents, зберігається в колонці description через Incident.Description, повертається через JSON-поле description відповіді GET /api/incidents/{id} і передається у #description.innerHTML у функції load файла src/SecureLab.Api/Client/browser-lab.js, рядок 10, як HTML-контекст. Тому, якщо опис містить рядок, схожий на HTML-тег, браузер інтерпретує дані не як звичайний текст, а як розмітку і може створити активні елементи DOM. Ні сервер, ні клієнт не перетворюють значення на безпечний текст: сервер лише виконує Trim і перевіряє довжину до 4000 символів.

Команди reset/start:

    docker compose --env-file infra/.env.example -f infra/compose.yaml up -d --wait
    dotnet run --project src/SecureLab.Api -- --reset-database
    dotnet test tests/SecureLab.Api.Tests/SecureLab.Api.Tests.csproj --configuration Release
    dotnet run --project src/SecureLab.Api

Перед reset задається змінна SeedUsers__Password у тому самому терміналі; значення не наводиться.

Create/read: форма «Створити й прочитати» на http://localhost:5080/browser-lab.html → POST /api/incidents → GET /api/incidents/{id}; читання наявного запису — поле «Guid інциденту» і кнопка «Прочитати».

| Спостереження | До fix | Після fix | Посилання на доказ |
|---|---|---|---|
| Значення в БД/API | | | |
| DOM основного Description | Нейтральний опис — текст у #description; <b>жирний</b> текст — у #description створено елемент <b>; опис seed …0003 — частина тексту стала елементом <script> | | рис. 10, 16, 17 |
| Дія marker | | | |
| Апостроф/лапки, 5 < 7, Unicode, HTML-like рядок | Апостроф і лапки, 5 < 7 і 9 > 3, Київ — Ω відображено як є. <b>жирний</b> текст: слово «жирний» напівжирне, у DOM елемент <b>. Seed …0003: видно лише «Текст», решта опису стала елементом <script>, який не виконався. Висновок: sink у browser-lab.js, р. 10 розбирає дані як HTML | | рис. 12–14, 16, 17 |
| Довжина за власними правилами ЛР 2 | Опис довжиною 1000 символів створено: POST 201, GET 200, текст відображено повністю. Межа — 4000 символів: Lab02Endpoints.cs, р. 79–81, і HasMaxLength 4000 у SecureLabDbContext.cs | | рис. 15 |
| Вікно Morgan: GET і перевірка marker; контроль Bob | | | |

Finding: буде заповнено в Етапі 2. Повторювана REG-XSS-01, негативний контроль і очікування: буде заповнено в Етапі 4.

## CORS і REG-CORS-01 (Для всіх)

Прогноз (Етап 3, крок 1), заповніть до зміни policy:

| Сценарій | Чи доходить request до API? | Чи може JavaScript прочитати response? | Чому |
|---|---|---|---|
| same-origin 5080 → API | | | |
| simple GET 4173 → API | | | |
| simple GET 4174 → API | | | |
| curl/.http → API | | не застосовується | |
| preflighted 4173 → API («Добре / Відмінно») | | | |

Команди запуску probes: … Policy — лише Development, один exact origin 4173; 4174 — контроль. Для «Задовільно» без AllowCredentials().

| Клієнт / простий запит | До policy | Fixed state | Status, ACAO, Console / доказ |
|---|---|---|---|
| Same-origin 5080 | | | |
| Allowed 4173 | | | |
| Disallowed 4174 | | | |
| Direct HTTP / curl | | | |

REG-CORS-01: browser evidence і дві actual HTTP-відповіді для Origin 4173/4174: … Перевірка порядку через читання 401 без cross-origin cookie: … Пояснення різниці browser/direct HTTP: …

## Додатково для Добре та Відмінно

Другий flow IncidentComment.Text: … Public seed comment Guid/текст, похідні counts за потреби, DOM до/після: … Єдиний safe helper і виклики для обох полів: …

Preflight з 4173 і 4174, OPTIONS → GET / blocked GET, exact origin/header/method: … Cookie-дослід 4173/4174/без AllowCredentials()/401 без входу: … Кінцевий AllowCredentials() — лише Development policy для 4173. Review XSS/CORS/CSRF/authorization — посилання на DEL-03: …

| Директива CSP | Фактичні ресурси застосунку | Обране джерело / причина |
|---|---|---|
| default-src | | |
| script-src | | |
| style-src | | |
| connect-src | | |
| img-src | | |
| frame-ancestors | | |

Місце зміни middleware: … Enforcement: document 200 і фактичний CSP header; три сторінки flow; кероване порушення new Image() до 4173 — результат: … Для «Добре» докази report-only не обов’язкові.

| Header / control | Фактичне значення | Ризик і межа захисту / policy rationale |
|---|---|---|
| X-Content-Type-Options | | |
| Referrer-Policy | | |
| frame-ancestors | | |

## Додатково для Відмінно

DOM-тест: файл/команда, browser, vulnerable commit і FAIL, fixed commit і PASS: … Comments, повторний render без дублювання та позитивні рядки: … CSP report-only → enforcement: фактичні headers, flow і кероване порушення в обох режимах: … safe-url.js / атрибут: … Пояснення text/attribute/URL/JavaScript та ризик third-party scripts — посилання на DEL-03: …

Додаткових DEL, навчальних демо або нового журналу не подавайте. Перед поданням повторіть чекліст для свого накопичувального рівня.
