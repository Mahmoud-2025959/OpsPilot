# OpsPilot.Infrastructure

> المشروع: `OpsPilot.Infrastructure` — طبقة البنية التحتية في الـ Clean Architecture على `.NET 10`.

## نبذة / Overview

طبقة تنفيذ الواجهات والتعامل مع الأدوات الخارجية: قاعدة البيانات (EF Core)، المصادقة (Identity)، الإشعارات (Email عبر MailKit)، ناشر الرسائل (RabbitMQ كخيار)، والوظائف المجدولة (Background Jobs / Hangfire كخيار). الكلاسيكس هنا بتنفّذ الـ Abstractions اللي معرّفة في `OpsPilot.Application`.

Implements the abstractions defined in the Application layer and talks to external systems (DB, Identity, Notifications, an optional RabbitMQ transport, and background jobs).

## المراجع الحالية / Current References

| المشروع المرجع | السبب / Reason |
|---|---|
| **OpsPilot.Application** | تنفيذ الواجهات (Abstractions) الخاصة بمنطق الأعمال |
| **OpsPilot.Domain** | استخدام الكيانات مع EF Core وتنفيذ الـ Repositories |
| **OpsPilot.Contracts** | رسائل الـ Messaging وموديلات التبادل |

## الحزم / NuGet Packages

| الحزمة | الإصدار | ملاحظات |
|---|---|---|
| `Microsoft.EntityFrameworkCore` + `.SqlServer` | `10.0.12` | الـ Persistence |
| `Microsoft.EntityFrameworkCore.Tools` | `10.0.12` | أدوات الـ migrations |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | `10.0.12` | المصادقة والصلاحيات |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | `10.0.12` | مصادقة التوكن |
| `MailKit` | `4.18.1` | إرسال الإيميل عبر SMTP |
| `RabbitMQ.Client` | `7.2.2` | ناشر الـ Outbox الحقيقي |
| `Hangfire.AspNetCore` + `.SqlServer` | `1.8.25` | جدولة الوظائف المتكرّرة |
| `StackExchange.Redis` | `3.3.0` | مرجع محفوظ للكاش المستقبلي (مش مستخدم في الكود) |
| `System.Security.Cryptography.Pkcs` | `10.0.13` | تبعية مثبَّتة لـ MailKit (إصدار أمني) |

## المجلدات / Folders

| المجلد | الدور / Purpose |
|---|---|
| `Persistence/` | الـ DbContext والـ Migrations ومستودعات التنفيذ (Repositories) + الـ interceptors |
| `RabbitMQ/` | ناشر الـ Outbox الحقيقي (`RabbitMqOutboxPublisher`) وإدارة الاتصال |
| `Redis/` | محفوظ للكاش المستقبلي — **لسه فاضي** حسب قرار الخطة (الكاش يتأجل لحد ما الأداء يطلبه) |
| `Identity/` | المصادقة والصلاحيات (Identity / JWT والـ token management) |
| `Notifications/` | إرسال الإشعارات وتنفيذ الـ Notification abstractions |
| `BackgroundJobs/` | الوظائف المجدولة (`SlaViolationDetectionWorker`, `OutboxProcessor`, `EmailDispatchWorker`) + `ScheduledJobs` لإطار Hangfire |
| `Security/` | معالجة الـ API keys ومصادقتها، وتشفير النسخة المحفوظة من السر (`AesGcmApiKeySecretProtector`) |
| `Services/` | تنفيذات الخدمات (مثل `MailKitEmailSender`, `LoggingOutboxPublisher`) |

## ملاحظات إضافية / Additional Notes

- مساحة الأسماء: `OpsPilot.Infrastructure`
- التارجت: `net10.0` مع `Nullable` و `ImplicitUsings` مفعّلين
- ممنوع استيراد `OpsPilot.API` — تنقطع دايرة المرجع مع الـ Web layer.
- **Redis**: المرجع موجود لكن مش مستخدم — قرار الخطة إن الكاش يتأجل لحد ما قياس الأداء يثبته.
- **RabbitMQ**: الناشر الحقيقي بيتسجّل بس لما `RabbitMq:Enabled=true`، وغير كده الـ `LoggingOutboxPublisher` هو اللي شغال.
- **Hangfire**: بيتسجّل بس لما `Hangfire:Enabled=true`، وغير كده الـ workers بتشتغل بمؤقتاتها الخاصة.