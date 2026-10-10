# OpsPilot.Infrastructure

> المشروع: `OpsPilot.Infrastructure` — طبقة البنية التحتية في الـ Clean Architecture على `.NET 10`.

## نبذة / Overview

طبقة تنفيذ الواجهات والتعامل مع الأدوات الخارجية: قاعدة البيانات (EF Core)، الكاش (Redis)، الرسائل (RabbitMQ)، المصادقة (Identity)، الإشعارات (Notifications)، والوظائف المجدولة (Background Jobs). الكلاسيكس هنا بتنفّذ الـ Abstractions اللي معرّفة في `OpsPilot.Application`.

Implements the abstractions defined in the Application layer and talks to external systems (DB, Redis, RabbitMQ, Identity, Notifications, Background jobs).

## المراجع الحالية / Current References

- لا يوجد أي `ProjectReference` حالياً.
- No `ProjectReference` currently.

## المراجع المقترحة / Recommended References

| المشروع المرجع | السبب / Reason |
|---|---|
| **OpsPilot.Application** | تنفيذ الواجهات (Abstractions) الخاصة بمنطق الأعمال |
| **OpsPilot.Domain** | استخدام الكيانات مع EF Core وتنفيذ الـ Repositories |
| **OpsPilot.Contracts** | رسائل الـ Messaging وموديلات التبادل |

## الحزم / NuGet Packages

| الحزمة | الإصدار | ملاحظات |
|---|---|---|
| — | — | لا يوجد أي `PackageReference` حالياً. No packages currently.

**أمثلة على حزم مقترحة مستقبلًا / Suggested future packages:** `Microsoft.EntityFrameworkCore` + `Microsoft.EntityFrameworkCore.SqlServer` (Persistence), `StackExchange.Redis` (Redis), `RabbitMQ.Client` (RabbitMQ), `Microsoft.AspNetCore.Identity` (Identity), `Hangfire` (Background Jobs), `SendGrid`/`MailKit` (Notifications), `Microsoft.Extensions.DependencyInjection`.

## المجلدات / Folders

| المجلد | الدور / Purpose |
|---|---|
| `Persistence/` | الـ DbContext واشكال الـ Migrations ومستودعات التنفيذ (Repositories) |
| `Redis/` | الكاش والتخزين المؤقت وتنفيذ الـ caching abstractions |
| `RabbitMQ/` | الـ Publishers / Consumers ورسائل الحداث |
| `Identity/` | المصادقة والصلاحيات (Identity / JWT والـ token management) |
| `Notifications/` | إرسال الإشعارات (Email / Push) وتنفيذ الـ Notification abstractions |
| `BackgroundJobs/` | الوظائف المجدولة والـ recurring jobs |

## ملاحظات إضافية / Additional Notes

- مساحة الأسماء: `OpsPilot.Infrastructure`
- التارجت: `net10.0` مع `Nullable` و `ImplicitUsings` مفعّلين
- ممنوع استيراد `OpsPilot.API` — تنقطع دايرة المرجع مع الـ Web layer.