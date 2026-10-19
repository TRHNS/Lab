# Матриця доступу ЛР04
| Дія | Рівень | Анонім | Reporter | Analyst | Administrator | Власник ресурсу має значення? | Стан реалізації |
|---|---|---:|---:|---:|---:|---:|---|
| Перегляд списку | добрий | deny, 401 | own | all | own | Так для `Reporter` і `Administrator` | Реалізувати; A-05 |
| Пошук `GET /api/incidents/search` | відмінний (A-11) | не виконувалось — рівень «Відмінно» | не виконувалось — рівень «Відмінно» | не виконувалось — рівень «Відмінно» | не виконувалось — рівень «Відмінно» | не виконувалось — рівень «Відмінно» | не виконувалось — рівень «Відмінно» |
| Зведення `GET /api/incidents/severity-summary` | відмінний (A-11) | не виконувалось — рівень «Відмінно» | не виконувалось — рівень «Відмінно» | не виконувалось — рівень «Відмінно» | не виконувалось — рівень «Відмінно» | не виконувалось — рівень «Відмінно» | не виконувалось — рівень «Відмінно» |
| Перегляд деталей | достатній: entry-контракт ЛР 5; головна action | deny, 401 | owner — allow; non-owner — deny, 404 | allow для аналізу інциденту | owner — allow; non-owner — deny, 404 | Так | Реалізувати; A-01, A-02, A-03, A-03R, A-10 |
| Створення інциденту | успадкований baseline | deny, 401 | allow for self | allow for self | allow for self | Сервер призначає власника | Перевірити baseline |
| Update content | добрий | deny, 401 | owner — allow (title, description, occurredAtUtc); non-owner — deny, 404 | deny, 403 | owner — allow; non-owner — deny, 404 | Так | Реалізувати; A-04, A-08a |
| Delete | добрий | deny, 401 | owner — allow, якщо статус New, інакше deny, 409; non-owner — deny, 404 | deny, 403 | owner — як Reporter; non-owner — deny, 404 | Так | Реалізувати; A-04 |
| Зміна severity/status (`POST /api/incidents/{id}/triage`) | добрий | deny, 401 | deny, 403 | allow лише New → Triaged; інший статус — deny, 409 | deny, 403 | Ні | Реалізувати; A-06, A-07, A-09 |
| Перегляд internal comment (details-проєкція і `GET /api/incident-comments/{id}`) | добрий | deny, 401 | deny, 404; у details не показується навіть власнику | allow після авторизації для parent incident | deny, 404 | Ні для internal rule; доступ до parent incident перевіряється окремо | Реалізувати; A-12 |
| Export/audit | відмінний, якщо flow існує | не застосовується | не застосовується | не застосовується | не застосовується | не застосовується | не застосовується |

Child resource: `IncidentComment` (`GET /api/incident-comments/{id}`) — доступ лише за правом читання parent incident; для non-owner — 404.
Політика відмов: authenticated non-owner для чужого інциденту отримує 404, ідентичний неіснуючому, — однаково для details, update, delete і child `GET /api/incident-comments/{id}`; так факт існування чужого ресурсу не розкривається. 403 — ресурс видимий, але дія заборонена або немає ролі; 409 — недопустимий поточний статус; анонім — 401 без redirect.