# F-02. Дозволений перехід статусу (triage) не має audit trail

**Статус:** Fixed
**Актив:** відстежуваність привілейованої зміни Incident.Status (хто, що, коли)
**Компонент:** `POST /api/incidents/{id}/triage` у `src/SecureLab.Api/Scaffolding/Lab02Endpoints.cs`
**Пріоритет:** P2

## Передумови і межі огляду

Commit `13fe617`; Development; reset БД; користувачі alice (Reporter) і morgan (Analyst);
новий інцидент alice у статусі New. Поза межами: інші маршрути зміни стану, ротація журналів.

## Відтворення

1. alice створює інцидент → 201, статус New.
2. alice: `POST /api/incidents/{id}/triage` з валідним antiforgery-токеном → 403;
   у журналі є `Audit authorization.denied … result denied`.
3. morgan: той самий запит → 200, статус Triaged.
   Очікувано: audit-подія з subject, action, resource, result, time.
   Фактично: у журналі немає жодного запису про успішний перехід; у `incident_status_history`
   нового рядка немає.

## Вплив і ймовірність

**Статус перевірки:** спостережено локально (огляд коду й журналу, етап 3 п. 4).

Фіксується лише невдала спроба, а сама привілейована зміна – ні. Неможливо встановити, хто й
коли перевів інцидент у Triaged, або розслідувати зловживання роллю Analyst. Даних не
розкрито, тому impact Medium (цілісність і підзвітність); перехід виконує кожен Analyst у
штатному сценарії, тому likelihood High → P2.

## Першопричина

Handler triage логував лише гілку відмови; гілка успіху змінювала `incident.Status` і зберігала
зміни без запису в `IncidentStatusHistory` і без audit-події.

## Виправлення

У гілці успіху додано запис `IncidentStatusHistory` (ChangedByUserId, Note `triage`, IncidentId,
OldStatus → NewStatus, CreatedAtUtc) в тій самій транзакції `SaveChangesAsync` та
structured audit-подію `incident.status_changed` з тими самими полями й trace, без тіла
запиту, cookie й токенів. Fixed commit `60beb44`.

## Retest

Кроки 1–3 на `60beb44`: крок 3 → 200, у `incident_status_history` рівно один запис
(ChangedByUserId = MorganId, Note = triage, New → Triaged, поточний час); у журналі
`Audit incident.status_changed … result allowed, New -> Triaged` (рис. 39).

## Positive regression

Крок 2 як і раніше → 403, статус New, запису в історії немає; повторний triage Analyst → 409.

## Залишковий ризик

Audit-подія в журналі зберігається лише в console sink без захисту від зміни; інші
змінювальні маршрути (PUT, DELETE) окремого audit trail не мають.

## Доказ

Тест `AuditTrailTests.Triage_DeniedReporter_NoChange_AllowedAnalyst_WritesAuditRecord`
(REG-AUDIT-01), команда `dotnet test SecureLab.sln -c Release`, результат Passed;
фактичні рядки журналу – рис. 39.
