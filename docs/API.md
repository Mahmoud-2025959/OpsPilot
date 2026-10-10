# OpsPilot.API

> المشروع: `OpsPilot.API` — طبقة الدخول (Web API) في الـ Clean Architecture على `.NET 10`.

## نبذة / Overview

بيتدخل التطبيق (Entry Point): الـ Controllers بتستقبل طلبات الـ HTTP، والـ Middleware بيعالج الأخطاء والمصادقة، والـ Extensions بتسجّل الخدمات في الـ DI (Composition Root). اهنا بيتم تجميع كل الطبقات وتسجيل المراجع في الـ `Program.cs`.

The entry point — Controllers receive HTTP requests, Middleware handles errors/auth, Extensions wire up services (Composition Root).

## المراجع الحالية / Current References

- لا يوجد أي `ProjectReference` حالياً.
- No `ProjectReference` currently.

## المراجع المقترحة / Recommended References

| المشروع المرجع | السبب / Reason |
|---|---|
| **OpsPilot.Application** | استدعاء الـ Use Cases (Mediator / Dispatchers) |
| **OpsPilot.Contracts** | خريطة الـ DTOs لطلبات واستجابات الـ API |
| **OpsPilot.Infrastructure** | تسجيل الخدمات (DI) — الـ Composition Root |

## الحزم / NuGet Packages

| الحزمة | الإصدار | ملاحظات |
|---|---|---|
| `Microsoft.AspNetCore.OpenApi` | `10.0.12` | دعم الـ OpenAPI الوثائق |

## الملفات الحالية / Current Files

| الملف | الدور / Purpose |
|---|---|
| `Program.cs` | إعداد الـ DI وإنجاز الـ Request Pipeline |
| `appsettings.json` | الإعدادات العامة للتطبيق |
| `appsettings.Development.json` | إعدادات بيئة التطوير |
| `Properties/launchSettings.json` | ملف التشغيل (http: `localhost:5093` / https: `localhost:7182`) |
| `OpsPilot.API.http` | ملف تجربة الـ Endpoints داخل الـ IDE |

## المجلدات / Folders

| المجلد | الدور / Purpose |
|---|---|
| `Controllers/` | الـ API endpoints (Incidents, Services, Users...) |
| `Middleware/` | معالجة استثناءات مركزية وحماية الطلبات |
| `Extensions/` | امتدادات `IServiceCollection` لتسجيل الخدمات (DI) |

## ملاحظات إضافية / Additional Notes

- مساحة الأسماء: `OpsPilot.API`
- التارجت: `net10.0` — SDK: `Microsoft.NET.Sdk.Web` مع `Nullable` و `ImplicitUsings`
- الإنطلاق: `dotnet run` داخل مجلد المشروع (http يشتغل على `5093`، إعدادات الـ https على `7182`)