# F-01. Відповідь 500 надсилається без заголовків безпеки

**Статус:** Fixed
**Актив:** захисні заголовки браузера (nosniff, Referrer-Policy, CSP) на всіх класах відповідей
**Компонент:** middleware заголовків безпеки в `src/SecureLab.Api/Program.cs`
**Пріоритет:** P2

## Передумови і межі огляду

Review-start commit `a2441db`; production-like profile (`ASPNETCORE_ENVIRONMENT=Production`,
`--no-launch-profile`, `-c Release`, http://localhost:5080); connection string на навмисно
недосяжний порт 54399 (`ConnectionStrings__SecureLab`), щоб викликати контрольовану 500.
Анонімний клієнт curl. Поза межами: інші причини 500, reverse proxy, HTTPS.

## Відтворення

1. Для порівняння у Development: `GET /api/incidents/9999…` (alice) → 404 Problem Details,
   у відповіді є `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`,
   `Content-Security-Policy`.
2. Запустити API в Production з недосяжною БД.
3. `POST /api/auth/login` з тілом `{"userName":"alice","password":"NotARealPassword1!"}`.
   Очікувано: 500 Problem Details із тими самими заголовками безпеки.
   Фактично: 500 `application/problem+json` з traceId, але **без** nosniff, Referrer-Policy і CSP.

## Вплив і ймовірність

**Статус перевірки:** спостережено локально (звіт, рис. 6 і рис. 10).

Відповідь на помилку – єдиний клас відповідей, де браузерні захисти вимикаються: MIME sniffing,
витік referrer і відсутність frame-ancestors саме тоді, коли застосунок у нештатному стані.
Тіло 500 не містить даних користувача, тому impact Low. Будь-яка 500 (збій БД, необроблений
виняток) відтворює стан без автентифікації, тому likelihood Medium → P2.

## Першопричина

Middleware встановлював заголовки до `await next(context)`. При винятку
`ExceptionHandlerMiddleware` викликає `Response.Clear()`, що видаляє вже встановлені заголовки,
і пише Problem Details без них.

## Виправлення

Заголовки встановлюються в `context.Response.OnStarting(...)`, тобто безпосередньо перед
надсиланням будь-якої відповіді, включно з 500. Значення заголовків і виняток для `/scalar` у
Development не змінено. Fixed commit `13fe617`, `src/SecureLab.Api/Program.cs`.

## Retest

Кроки 2–3 на `13fe617`: 500 `application/problem+json` тепер містить `X-Content-Type-Options: nosniff`,
`Referrer-Policy: no-referrer` і `Content-Security-Policy` з `frame-ancestors 'none'`; тіло
відповіді не змінилось (звіт, рис. 15).

## Positive regression

Крок 1 і решта класів відповідей (200 text/html, 200 json, 400, 404) зберігають ті самі заголовки;
звичайний flow клієнта працює без CSP-порушень у Console.

## Залишковий ризик

Перевірено лише контрольовану 500 через недосяжну БД; відповіді, сформовані поза конвеєром
застосунку (reverse proxy, Kestrel до middleware), не перевірялись.

## Доказ

Тест `ProductionProfileTests.UnexpectedError_KeepsSecurityHeaders`, команда
`dotnet test SecureLab.sln -c Release`, результат Passed. Ручний trace – рис. 10 (до) і рис. 15 (після).
