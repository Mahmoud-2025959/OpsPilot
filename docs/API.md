# OpsPilot.API

> المشروع: `OpsPilot.API` — طبقة الدخول (Web API) في الـ Clean Architecture على `.NET 10`.

## نبذة / Overview

بيتدخل التطبيق (Entry Point): الـ Controllers بتستقبل طلبات الـ HTTP، والـ Middleware بيعالج الأخطاء والمصادقة، والـ Extensions بتسجّل الخدمات في الـ DI (Composition Root). اهنا بيتم تجميع كل الطبقات وتسجيل المراجع في الـ `Program.cs`.

The entry point — Controllers receive HTTP requests, Middleware handles errors/auth, Extensions wire up services (Composition Root).

## المراجع الحالية / Current References

| المشروع المرجع | السبب / Reason |
|---|---|
| **OpsPilot.Application** | استدعاء الـ Use Cases عبر الـ Controllers |
| **OpsPilot.Contracts** | عقود الـ DTOs لطلبات واستجابات الـ API |
| **OpsPilot.Infrastructure** | تسجيل الخدمات (DI) — الـ Composition Root |

## الحزم / NuGet Packages

| الحزمة | الإصدار | ملاحظات |
|---|---|---|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | `10.0.12` | مصادقة التوكن |
| `Microsoft.AspNetCore.Mvc.Versioning` | `5.1.0` | نسخ الـ API (`v1`) |
| `Microsoft.AspNetCore.OpenApi` | `10.0.12` | دعم الـ OpenAPI |
| `Microsoft.EntityFrameworkCore.Design` | `10.0.12` | أدوات الـ design-time (الـ migrations) |
| `Serilog.AspNetCore` | `10.0.0` | تسجيل الأحداث |
| `Serilog.Sinks.Seq` | `9.1.0` | شحن اللوجات لـ Seq |
| `Swashbuckle.AspNetCore` | `10.3.0` | واجهة Swagger UI |
| `Hangfire.AspNetCore` | `1.8.25` | لوحة مراقبة الوظائف المجدولة |
| `System.Security.Cryptography.Pkcs` | `10.0.13` | تبعية مثبَّتة لـ MailKit (إصدار أمني) |

## الملفات الحالية / Current Files

| الملف | الدور / Purpose |
|---|---|
| `Program.cs` | إعداد الـ DI وإنجاز الـ Request Pipeline |
| `appsettings.json` | الإعدادات العامة للتطبيق — والقيم السرّية فيها فاضية |
| `appsettings.Development.json` | إعدادات بيئة التطوير — وفيها مفاتيح التطوير (مفتاح توقيع الـ JWT ومفتاح تشفير مفاتيح الـ ingestion). التطبيق بيقف عند الإقلاع لو المفتاح مش موجود |
| `Properties/launchSettings.json` | ملف التشغيل (http: `localhost:5093` / https: `localhost:7182`) |
| `OpsPilot.API.http` | ملف تجربة الـ Endpoints داخل الـ IDE |

## المجلدات / Folders

| المجلد | الدور / Purpose |
|---|---|
| `Controllers/` | الـ API endpoints — 14 controller (Incidents, Services, Users, Teams, Auth, Events, ApiKeys, AuditLogs, Notifications, NotificationPreferences, SlaPolicies, Analytics, Roles, Permissions) |
| `Hubs/` | `IncidentsHub` للبث اللحظي عبر SignalR |
| `Middleware/` | معالجة استثناءات مركزية، CorrelationId، وحل الـ Tenant |
| `RateLimiting/` | حد المعدل على نقطة استقبال الأحداث (429) |
| `Realtime/` | `IncidentsHubNotifier` — تنفيذ `IIncidentNotifier` |
| `Scheduling/` | تسجيل الوظائف المجدولة ولوحة Hangfire |
| `Health/` | فحوصات الجاهزية (`DatabaseHealthCheck`) |
| `Extensions/` | امتدادات `IServiceCollection` لتسجيل الخدمات (DI) |
| `wwwroot/` | ملفات ثابتة — فيها `signalr-test.html` (أداة تطوير للـ Hubs) |

## المفاتيح السرّية / Secrets

المفاتيح السرّية مش موجودة في `appsettings.json` — المكان ده فيه placeholders فاضية، والتطبيق بيقف عند الإقلاع لو أي مفتاح ناقص. القيم بتوصل من `appsettings.Development.json` في التطوير، ومن الـ environment في الـ deployment.

| المفتاح | متغير البيئة | إيه اللي بيعمله |
|---|---|---|
| `Jwt:SigningKey` | `JWT__SIGNINGKEY` | توقيع والتحقق من الـ access tokens (HMAC-SHA256، 32 بايت على الأقل) |
| `ApiKeyEncryption:Key` | `APIKEYENCRYPTION__KEY` | تشفير النسخة المحفوظة من مفاتيح الـ ingestion (AES-256-GCM، 32 بايت) |
| `ApiKeyEncryption:PreviousKeys` | `APIKEYENCRYPTION__PREVIOUSKEYS` | مفاتيح قديمة بتتقبل في فك التشفير بس مش في التشفير — عشان تغيير المفتاح ما يبقاش محتاج data migration |

التوليد: `openssl rand -base64 32`. الـ `dotnet ef` بيقلع الـ host وبيلزمه `ASPNETCORE_ENVIRONMENT=Development` عشان يلاقي المفاتيح.

## الأدوار والصلاحيات / Roles and permissions

الأدوار خاصة بالشركة (ERD §3) والكتالوج عام. القراءة مفتوحة لأي مستخدم مسجّل، والتعديل يحتاج `roles.change`.

| Endpoint | الوصف / Purpose | الصلاحية / Policy |
|---|---|---|
| `GET /api/v1/permissions` | كتالوج الصلاحيات الثابت (كل الأسماء اللي الـ build عارفها) | أي مستخدم مسجّل |
| `GET /api/v1/roles` | أدوار الشركة، ومع كل دور صلاحياته | أي مستخدم مسجّل |
| `GET /api/v1/roles/{id}` | دور واحد وصلاحياته | أي مستخدم مسجّل |
| `POST /api/v1/roles` | إنشاء دور مخصص (اسم، وصف، صلاحيات) | `roles.change` |
| `PUT /api/v1/roles/{id}` | تعديل اسم/وصف دور مخصص | `roles.change` |
| `DELETE /api/v1/roles/{id}` | إنهاء دور مخصص (soft delete) — بشرط ما يكونش فيه مستخدمين شايلينه | `roles.change` |
| `PUT /api/v1/roles/{id}/permissions` | استبدال صلاحيات الدور بالكامل (اللي ما يتذكرش يتشال) | `roles.change` |

القواعد المطبَّقة على الـ endpoints دي:

- أدوار الـ System الخمسة (`Owner`, `Admin`, `TeamLead`, `Engineer`, `Viewer`) ما تتrenameش ولا تتنهى.
- صلاحيات دور الـ **Owner** متجمّدة — مفيش حد يقدر يعدّلها.
- **منع رفع الصلاحيات الذاتي**: مفيش حد يقدر يدّي دور فيه صلاحية هو نفسه ما يملكهاش. الـ **Owner** وحده استثناء، لأنه أصل الشركة (يقدر يعرف أي صلاحية في الكتالوج).
- **دور الـ Owner ميتوزّعش غير من Owner**: أي محاولة من غير Owner تدي حد دور `Owner` — سواء وقت إنشاء مستخدم (`POST /users`) أو وقت تغيير أدواره (`PUT /users/{id}/roles`) — بترجع `403`. الحماية دي مهمة لأن `Admin` عنده `roles.change` بس مش عنده `tenant.settings`.
- **الدور اللي لسه شايله مستخدمين ما يتنهاش**: `DELETE` بترجع `409 Conflict`، ولازم المستخدمين يتنقلوا لدور تاني الأول (قاعدة ERD §3).
- كل تغيير بيتسجل في الـ Audit: `Role.Changed` (إنشاء/تعديل/إنهاء) و `Role.PermissionsChanged` (تغيير الصلاحيات).
- الصلاحيات بتتحمل كـ Claims في الـ Token — التغيير بيبان بعد تجديد الـ Token (refresh). عمر الـ access token الافتراضي **15 دقيقة**.

استجابات المستخدمين (`GET /users`, `GET /users/{id}`, و `GET /users/me`) بتحتوي على `permissions` — الصلاحيات الفعلية اللي المستخدم واخدها من كل أدواره (بعد استبعاد الأدوار المتهانة).

| Endpoint | الوصف / Purpose | الصلاحية / Policy |
|---|---|---|
| `GET /api/v1/users/{id}/permissions` | الصلاحيات الفعلية لمستخدم + الأدوار اللي جابتها (بدون `users.manage` مش هتقدر) | `users.manage` |

## ملاحظات إضافية / Additional Notes

- مساحة الأسماء: `OpsPilot.API`
- التارجت: `net10.0` — SDK: `Microsoft.NET.Sdk.Web` مع `Nullable` و `ImplicitUsings`
- الإنطلاق: `dotnet run` داخل مجلد المشروع (http يشتغل على `5093`، إعدادات الـ https على `7182`)
- نقاط الفحص (health): `/health`, `/health/live`, `/health/ready`
- مكتبة الـ Hangfire (`/jobs`) تظهر فقط لما `Hangfire:Enabled` يكون مفعّلًا.
- **أداة الـ SignalR** (`/signalr-test`) تظهر في **Development بس** — بتوصّل بالـ Hub
  بنفس التوكن وتعرض الرسايل الحية لعرض الاتجاهين (API→Hub و Hub→Hub).
