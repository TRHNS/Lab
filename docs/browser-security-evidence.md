# Докази безпеки браузера (DEL-01)

Рівень: добрий «Добре». Рівні накопичувальні: виконано обов'язковий результат для всіх і доповнення рівня «Добре». Паролі, cookie/token values і export browser storage не наводяться. Номери рисунків відповідають звіту DEL-03.

## Стенд і Git-стан (Для всіх)

ОС: Windows, термінал Git Bash. Browsers: Opera 138.0.6044.45 для Етапів 1–2, Microsoft Edge 156 для Етапів 3–4. .NET SDK: 10.0.303. API origin: http://localhost:5080. Probe origins: http://localhost:4173 — дозволений, http://localhost:4174 — контрольний недозволений.

Release: lab-05-start-v1, inherits: v0.4.0. base_commit і entry_parent: 967b2055748c1a0f2856fd928fd72b9db4a09580; commit політики SDK не робився. Гілка lab/5-browser-xss від v0.4.0, рис. 1–2.

Commits: vulnerable 5197a75 — scaffold, рис. 2; seed-коментар з marker — окремий commit; fixed XSS 8b8214bd83666ee11ff3f3143e4aea0c0433adda, рис. 36; CORS policy b31a93002bef033b30b5d32730a3cc1ba802a588, рис. 51; CSP і перенесення inline-стилів [HASH CSP], рис. 57. Тег v0.5.0 стоїть на фінальному commit з цим файлом; main оновлено fast-forward; передано через приватний GitHub remote.

Механізм сеансу: cookie .AspNetCore.Identity.Application; Domain localhost; Path /; Session; HttpOnly — так; SameSite — Lax; Secure — ні, бо стенд працює по HTTP. Value не наводиться, рис. 11. HttpOnly не дає скрипту прочитати cookie, але не заважає виконанню впровадженого коду від імені користувача.

## Гіпотеза й REG-XSS-01 (Для всіх)

Карта Description і Comment: docs/source-to-sink.md.

Гіпотеза: значення з поля форми <textarea name="description"> на browser-lab.html надсилається як JSON-поле description у POST /api/incidents, зберігається в колонці description через Incident.Description, повертається через JSON-поле description у GET /api/incidents/{id} і передається у #description.innerHTML у функції load файла src/SecureLab.Api/Client/browser-lab.js, рядок 10, як HTML-контекст. Тому HTML-подібний рядок браузер розбирає як розмітку. Сервер лише виконує Trim і перевіряє довжину до 4000 символів. Гіпотезу підтверджено, рис. 16–21.

Передумови: локальний стенд; користувачі alice — Reporter, morgan — Analyst, bob — Reporter; вхід через /auth-lab.html → Login: 204, рис. 5. Нейтральний запис f8fa7d57-03c2-4f07-a11c-d5d4ceaea9fc, рис. 7–10. Marker скопійовано без змін з однорядкового resources/lab05-marker.txt. Запис з marker до виправлення: 25ccdd57-e604-4450-b824-a7f7fd455718. Після reset для seed-коментаря запис створено повторно тією самою процедурою: f10c600f-fe35-46e9-93e0-11074ed2c99f.

Команди reset/start:

    docker compose --env-file infra/.env.example -f infra/compose.yaml up -d --wait
    export SeedUsers__Password='…'   # значення поза Git
    dotnet run --project src/SecureLab.Api -- --reset-database
    dotnet test tests/SecureLab.Api.Tests/SecureLab.Api.Tests.csproj --configuration Release
    dotnet run --project src/SecureLab.Api

Create/read: форма «Створити й прочитати» на /browser-lab.html → POST /api/incidents → GET /api/incidents/{id}; читання наявного запису — поле «Guid інциденту» і кнопка «Прочитати».

| Спостереження | До fix | Після fix | Посилання на доказ |
|---|---|---|---|
| Значення в БД/API | POST 201, GET 200; description у response JSON містить marker символ у символ | те саме значення символ у символ | рис. 23 → 32 |
| DOM основного Description | <b> став елементом; опис …0003 потрапив у <script>; marker створив img з onerror | лише текстовий вузол, без img і b | рис. 16–17, 20 → 31, 33 |
| Дія marker | атрибут null → 'executed' | атрибут null → null | рис. 19 → 30–31 |
| Апостроф/лапки, 5 < 7, Unicode, HTML-like рядок | звичайні значення відображались як є; <b> ставав елементом | усі значення, включно з <b>жирний</b>, відображено як текст | рис. 12–14, 16 → 33 |
| Довжина за власними правилами ЛР 2 | опис 1000 символів створено і відображено | опис 3900 символів, межа 4000; textContent.length = 3900 | рис. 15 → 34–35 |
| Вікно Morgan: GET і перевірка marker; контроль Bob | Morgan, окремий сеанс: GET 200, null → 'executed' — stored XSS у сеансі привілейованого глядача. Bob, не власник: GET 404, null → null | marker відображено як текст | рис. 21–22 |

Класифікація: stored XSS за походженням даних і DOM-based за місцем sink; окремого reflected flow немає.

Finding. Актив: сеанс користувача в браузері, зокрема Analyst Morgan. Передумови: обліковий запис Reporter і право створити інцидент. Першопричина: збережений текст записувався в innerHTML у browser-lab.js, р. 10 і р. 14. Вплив на стенді: лише локальна дія marker. Fix: єдиний helper setText через textContent для опису і коментарів, рис. 27. Retest: той самий marker відображено як текст, значення в JSON не змінилось, рис. 30–32. Залишковий ризик: інші поля й сторінки поза flow, сторонні скрипти, контексти URL і атрибутів. Фільтр слів чи кутових дужок не є виправленням, а CSP без виправлення sink не усуває першопричину.

REG-XSS-01: процедура описана в DEL-03, таблиця 6. Негативний контроль — vulnerable 5197a75, де marker виконувався. Під enforcement-CSP висновок робиться за DOM, а не лише за відсутністю дії marker.

## CORS і REG-CORS-01 (Для всіх)

Прогноз заповнено до зміни policy:

| Сценарій | Чи доходить request до API? | Чи може JavaScript прочитати response? | Чому |
|---|---|---|---|
| same-origin 5080 → API | так | так | той самий origin |
| simple GET 4173 → API | так | ні без коректної CORS-відповіді | SOP і CORS |
| simple GET 4174 → API | так | ні | origin не в allowlist |
| curl/.http → API | так | не застосовується | не браузерний JavaScript |
| preflighted 4173 → API | спочатку OPTIONS; actual GET залежить від preflight | ні без успішного preflight | browser preflight і CORS |

Команди запуску probes:

    py -3 -m http.server 4173 --bind localhost --directory demonstrations/cors-probe
    py -3 -m http.server 4174 --bind localhost --directory demonstrations/cors-probe

Policy Lab05BrowserProbe — лише Development, один exact origin http://localhost:4173, метод GET, заголовок X-Lab-Cors-Probe, AllowCredentials. 4174 — контроль. UseCors стоїть після UseRouting і перед UseAuthentication.

| Клієнт / простий запит | До policy | Fixed state | Status, ACAO, Console / доказ |
|---|---|---|---|
| Same-origin 5080 | працює | працює | CORS не потрібен |
| Allowed 4173 | 200, читання заблоковано; preflight без дозволу, actual GET не надіслано | 200 і ACAO http://localhost:4173; OPTIONS 204 → GET 200 | рис. 37–38 → 41–42 |
| Disallowed 4174 | так само заблоковано | заблоковано; OPTIONS 204 без Access-Control-Allow-… | рис. 43–44 |
| Direct HTTP / curl | 200 | 200 для будь-якого Origin; ACAO лише для 4173 | рис. 50, 59 |

REG-CORS-01: browser evidence — 4174 не отримує response, у Network є request до http://localhost:5080 з Origin 4174, рис. 43–44, 47. HTTP evidence з кінцевого стану: для Origin 4173 є Access-Control-Allow-Origin: http://localhost:4173 і Access-Control-Allow-Credentials: true, для 4174 жодного Access-Control-…; обидві відповіді 200 з nosniff, no-referrer і CSP, рис. 59. Перевірка порядку: скрипт з 4173 прочитав 401 без cookie, рис. 40, 49. Різниця browser/direct HTTP: SOP — правило браузера; curl читає відповідь завжди, але проходить ті самі перевірки API.

## Додатково для Добре та Відмінно

Другий flow IncidentComment.Text: карта в docs/source-to-sink.md. Public seed comment 30000000-0000-0000-0000-000000000005 від Morgan до інциденту Alice …0001 з текстом Lab05Marker, рис. 24. До fix: marker виконався, у #comments створено img, рис. 25–26. Після fix: текстовий вузол, атрибут null, рис. 28–29. Єдиний safe helper setText у browser-lab.js викликається для #description і для кожного p у #comments, рис. 27.

Preflight: з 4173 — OPTIONS 204 з Access-Control-Allow-Origin, -Methods: GET, -Headers: X-Lab-Cors-Probe, потім GET 200, рис. 42; з 4174 — OPTIONS 204 без дозвільних заголовків, actual GET не надіслано, рис. 43–44. Cookie-дослід: 4173 з credentials — 200 і Access-Control-Allow-Credentials: true, рис. 46; 4174 — запит з cookie виконано, 200, але відповідь не віддано скрипту, рис. 47; без AllowCredentials 4173 заблоковано, рис. 48; без входу — 401 з CORS-заголовками, рис. 49. Кінцевий AllowCredentials — лише Development policy для 4173. Review XSS/CORS/CSRF/authorization — DEL-03, Етап 3, крок 8.

| Директива CSP | Фактичні ресурси застосунку | Обране джерело / причина |
|---|---|---|
| default-src | запасне правило | 'self' |
| script-src | власні app.js, browser-lab.js, auth-lab.js | 'self' |
| style-src | власний styles.css; inline style="…" з index.html перенесено в нього | 'self', без 'unsafe-inline' |
| connect-src | fetch лише на 5080 | 'self'; probe 4173 не додано |
| img-src | зображень у flow немає | 'self' |
| frame-ancestors | вбудовування не потрібне | 'none', явно, бо немає fallback |

Місце зміни: наявна middleware заголовків у src/SecureLab.Api/Program.cs; виняток лише для /scalar у Development як залишковий ризик. Enforcement: document 200 і один заголовок Content-Security-Policy, рис. 53, 58; три сторінки flow без порушень, рис. 54–55; кероване порушення new Image() до 4173 заблоковано директивою img-src 'self', рис. 56. Report-only для «Добре» не використовувався.

| Header / control | Фактичне значення | Ризик і межа захисту / policy rationale |
|---|---|---|
| X-Content-Type-Options | nosniff | зменшує MIME sniffing; не замінює правильний Content-Type і validation |
| Referrer-Policy | no-referrer | не передає адресу сторінки іншим сайтам; клієнт працює лише з 5080, посилання OpenAPI має rel="noreferrer", тому суворіша policy нічого не ламає; не замінює заборону секретів у URL |
| frame-ancestors | 'none' | захищає від clickjacking; X-Frame-Options не додано, щоб не дублювати; не замінює авторизацію і виправлення XSS |

## Додатково для Відмінно

Не виконується: обрано рівень «Добре».

Додаткових DEL, навчальних демо або нового журналу не подавайте. Перед поданням повторіть чекліст для свого накопичувального рівня.