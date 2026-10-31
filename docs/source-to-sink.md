# Власна карта source-to-sink

Поле: Incident.Description. Стан/commit: vulnerable `5197a75`, гілка `lab/5-browser-xss`, від `v0.4.0`. Вхід/Guid запису: `alice`, інцидент `f8fa7d57-03c2-4f07-a11c-d5d4ceaea9fc` з нейтральним описом.

| Крок | Файл та функція/DTO | Конкретне поле/значення | Доказ і довіра |
|---|---|---|---|
| Source | `Client/browser-lab.html`, форма `#edit`, `<textarea name="description">` | текст, введений користувачем | рис. 7, 10; контрольоване користувачем |
| Request | `Client/browser-lab.js`, обробник `submit` форми `#edit`, р. 20–24, `fetch` методом POST на `/api/incidents` | JSON-поле `description` разом із `title`, `severity`, `occurredAtUtc` | рис. 8–9: 201 Created; контрольоване користувачем, `JSON.stringify` лише серіалізує |
| Validation/mapping | `Scaffolding/Lab02Endpoints.cs`, `MapPost` для `/api/incidents`, р. 62; DTO `CreateIncidentRequest`, р. 257–258; перевірка р. 79–81; `Trim` р. 95; `Description = desc` р. 123 | `request.Description` → `Incident.Description` | перевірено лише непорожність і довжину ≤ 4000, рис. 15; `Trim` прибирає пробіли, а не HTML; значення не стає trusted |
| Storage | `Data/Entities/Incident.cs:8`, властивість `Description`; `Data/SecureLabDbContext.cs:37` | колонка `description`, `HasMaxLength` 4000 | збережене значення користувача |
| Response | `Application/Incidents/IncidentQueries.cs`, `GetDetailsAsync` → `Presentation/Contracts/IncidentResponses.cs`, `IncidentDetailsResponse`; GET `/api/incidents/{id}` | JSON-поле `description` | рис. 8–9: 200 OK; недовірене для браузера, JSON-екранування не захищає HTML-sink |
| Client transform | `Client/browser-lab.js`, функція `load`, р. 8: `current = await response.json` | `current.description` без змін | дані передаються як є |
| Sink/context | `Client/browser-lab.js`, функція `load`, **р. 10**: `#description.innerHTML = current.description` | `<div id="description">` | **HTML-контекст, небезпечно**: рядок розбирає HTML parser, тож теги з опису стають елементами DOM; рис. 16 — створено `<b>`, рис. 17 — створено `<script>` |

## Інвентаризація

| Файл/функція | Дані | Категорія даних | Контекст | Рішення |
|---|---|---|---|---|
| `browser-lab.js`, `load`, р. 10 — `#description.innerHTML` | `current.description` | збережене значення користувача | HTML parser | **небезпечно** → замінити на `textContent` |
| `browser-lab.js`, `load`, р. 14 — `paragraph.innerHTML` | `comment.text` | збережене значення користувача | HTML parser | **небезпечно** → замінити на `textContent` |
| `browser-lab.js`, `load`, р. 9 — `#title.textContent` | `current.title` | збережене значення користувача | text node | безпечно |
| `browser-lab.js`, `load`, р. 17 — `elements.description.value` | `current.description` | збережене значення користувача | значення поля форми як текст | безпечно |
| `browser-lab.js`, р. 6 і 25 — `status.textContent` | `response.status` у шаблоні з literal | server-generated + trusted constant | text node | безпечно |
| `browser-lab.js`, обробник submit, р. 28 — `#incident-id.value` | `created.id` | server-generated | значення поля форми | безпечно |
| `browser-lab.js`, р. 2 і 5 — `fetch` на `/api/incidents/` з `id` | значення `#incident-id` | контрольоване користувачем | URL path | безпечно: `encodeURIComponent`, запит same-origin |
| `browser-lab.js`, р. 13 — `createElement` з literal `"p"` | literal `"p"` | trusted constant | ім'я тегу | безпечно |
## Другий потік: IncidentComment.Text (рівень «Добре»)

Поле: IncidentComment.Text. Запис: коментар 30000000-0000-0000-0000-000000000005 від Morgan до інциденту Alice 20000000-0000-0000-0000-000000000001.

| Крок | Файл та функція/DTO | Конкретне поле/значення | Доказ і довіра |
|---|---|---|---|
| Source | `Data/DbSeeder.cs`, `SeedAsync`, константа `Lab05Marker`, коментар …0005 | текст коментаря; у продукті його вводив би автор коментаря | рис. 24; контрольоване користувачем |
| Request | немає: endpoint створення коментарів в API відсутній | — | значення потрапляє в БД через відтворюваний seed |
| Validation/mapping | немає, seed записує значення напряму | `Text = Lab05Marker` | значення не стає trusted |
| Storage | сутність `IncidentComment.Text` у PostgreSQL | marker без змін | збережене значення користувача |
| Response | `Application/Incidents/IncidentQueries.cs`, `GetDetailsAsync` → `IncidentCommentResponse.Text`; GET `/api/incidents/{id}` | JSON-поле `comments[].text`; internal comments бачить лише Analyst | недовірене для браузера |
| Client transform | `Client/browser-lab.js`, функція `load`, цикл `for (const comment of current.comments ?? [])` | `comment.text` без змін | дані передаються як є |
| Sink/context до fix | `browser-lab.js`, р. 14: `paragraph.innerHTML = comment.text` | `<p>` у `#comments` | **HTML-контекст, небезпечно**; рис. 25–26 |
| Sink/context після fix | `browser-lab.js`: `setText(document.createElement("p"), comment.text)` | `<p>` у `#comments` | текстовий вузол, безпечно; рис. 28–29 |

Висновок: коментар проходить той самий шлях, що й опис, тому обидва поля виправлено одним helper `setText`, а не окремими латками.
