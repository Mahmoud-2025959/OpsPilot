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
| `Identity/` | المصادقة والصلاحيات (Identity / JWT والـ token management) |
| `Notifications/` | إرسال الإشعارات وتنفيذ الـ Notification abstractions |
| `BackgroundJobs/` | الوظائف المجدولة (`SlaViolationDetectionWorker`, `OutboxProcessor`, `EmailDispatchWorker`, `WebhookDispatchWorker`) + `ScheduledJobs` لإطار Hangfire |
| `Security/` | معالجة الـ API keys ومصادقتها، وتشفير النسخة المحفوظة من السر (`AesGcmApiKeySecretProtector`) |
| `Services/` | تنفيذات الخدمات (مثل `MailKitEmailSender`, `LoggingOutboxPublisher`) |

## ملاحظات إضافية / Additional Notes

- مساحة الأسماء: `OpsPilot.Infrastructure`
- التارجت: `net10.0` مع `Nullable` و `ImplicitUsings` مفعّلين
- ممنوع استيراد `OpsPilot.API` — تنقطع دايرة المرجع مع الـ Web layer.
- **Redis**: حزمة `StackExchange.Redis` موجودة كمرجع فقط، ومفيش أي كود بيستخدمها ولا مجلد `Redis/` — الكاش مؤجّل بقرار لحد ما قياس أداء يثبته (راجع قسم P3).
- **RabbitMQ**: الناشر الحقيقي بيتسجّل بس لما `RabbitMq:Enabled=true`، وغير كده الـ `LoggingOutboxPublisher` هو اللي شغال.
- **RabbitMQ محليًا (بدون Docker)**: فيه سكربتات محمولة تحت `scripts/` (Erlang + RabbitMQ في `%USERPROFILE%\Tools`) لتشغيل البروكر على Windows من غير صلاحيات أدمن؛ راجع `scripts/README.md`. لو Docker Desktop متاح، `docker compose up -d rabbitmq` هو المسار المفضّل.
- **Hangfire**: بيتسجّل بس لما `Hangfire:Enabled=true`، وغير كده الـ workers بتشتغل بمؤقتاتها الخاصة. سكيما `HangFire` بتتعمل تلقائيًا في نفس قاعدة بيانات التطبيق (`PrepareSchemaIfNecessary`)، والـ dashboard على `/jobs`. الوظائف المتكررة أربعة: `sla-violation-sweep`, `outbox-drain`, `email-dispatch`, `webhook-dispatch` — وكل واحدة بتستدعي نفس pass method اللي بيستدعيه مؤقت الـ worker. في Production الـ dashboard بيتطلب HTTP Basic (`Hangfire:DashboardUsername` / `DashboardPassword`)؛ من غير اسم مستخدم الطلب بيترفض. محليًا (Development واسم فاضي) الـ dashboard مفتوح.
- **Webhook dispatch**: `WebhookDispatchWorker` مسجّل `AddScoped` دايمًا عشان Hangfire يقدر يعمله resolve، و`AddHostedService` بس لما `Hangfire:Enabled=false` — ممنوع الاتنين مع بعض.
- **ترتيب الإقلاع**: `DatabaseMigratorHostedService.MigrateIfConfiguredAsync` بيتنادى قبل تشغيل الـ host، وتسجيل الـ recurring jobs بقى في `ScheduledJobsRegistrationHostedService` (بعد الـ migrator وبعد Hangfire server). قبل كده التسجيل كان بيحصل أثناء بناء الـ pipeline، فكان بيفشل على قاعدة فاضية لأن جدول الـ Hangfire لسه مش موجود.
- **SMTP**: MailKit. محليًا `Smtp:Enabled` في Development بيوجّه على `localhost:2525` (smtp4dev). مع Docker: profile `mail` في compose (UI على 3000). من غير Docker: `scripts\start-smtp4dev.ps1` بيشغّل نسخة محمولة (UI على 5001). `Enabled=false` ما بيحرّقش المحاولات. إعادة صفوف `Failed`: `scripts/reset-failed-emails.sql`.
- **نشر Docker**: `Dockerfile` متعدد المراحل (`net10.0`، مستخدم غير root، منفذ 8080). خدمة `api` في `docker-compose.yml` تعتمد على sqlserver و rabbitmq، وتعمل migrate عند الإقلاع (`Database:MigrateOnStartup`). Healthcheck على `/health/ready`. أسرار JWT/التشفير/الـ dashboard من `.env`.
- **Outbox**: الـ scope في `OutboxProcessor` لازم يتقفل بـ `CreateAsyncScope()` لأن ناشر RabbitMQ بيعتمد على `IAsyncDisposable` فقط؛ القفل المتزامن كان بيرمي استثناء ويفشل وظيفة `outbox-drain`.
- **أحداث الحوادث**: الإنشاء اليدوي والـ ingestion بيصدروا `IncidentCreated`، والتحولات والتعليقات والإسناد (لما المُسند يتغيّر) بيصدروا `IncidentEventAdded`. الرسالة والـ timeline entry بيتكتبوا في نفس الـ save، والـ webhook deliveries بتتـ stage معاهم لنفس الـ endpoints النشطة.

## مؤجَّل بقرار: P3 / Deferred by decision

البنود دي **خارج نطاق النسخة الحالية** عن قصد، مش ناقصة بالخطأ. القرار موثَّق هنا عشان يفضل مرجع لو حد سأل ليه مش موجودة.

| البند | الحالة | سبب التأجيل |
|---|---|---|
| **Frontend** | غير منفّذ | المشروع API-only: الـ Controllers والـ SignalR hub والـ DTOs هي الواجهة. بناء واجهة (Login / Incidents / Timeline / Notifications) أسبوع+ ومش مطلوب للتسليم الحالي |
| **Redis Caching** | غير مستخدم | مفيش قياس أداء بيثبت إن الكاش مطلوب؛ إضافته دلوقتي هتزيد تعقيد الـ invalidation بدون مكسب مقاس. الحزمة موجودة كمرجع فقط |
| **AI Suggestions** | كيان ومigration فقط | `AiSuggestion` متسجّل في الـ schema، لكن مفيش service ولا endpoint ولا provider. التنفيذ بيحتاج مفتاح API خارجي وقرار عن الخصوصية والتكلفة |

إعادة النظر في أي بند منهم قرار منفصل، وبيتطلب قياس أو متطلب واضح قبل ما يبدأ.