# OpsPilot.IntegrationTests

> المشروع: `OpsPilot.IntegrationTests` — اختبارات التكامل في الـ Clean Architecture على `.NET 10`.

## نبذة / Overview

اختبارات بتشغّل التطبيق مع الخدمات الحقيقية أو شبه الحقيقية (Testcontainers / WebApplicationFactory): قاعدة البيانات، RabbitMQ، الـ API نفسه via HTTP. بتيأكد إن الطبقات بتتوصل ببعض صح من الديفندنشي حتى الاستجابة.

Integration tests exercising the real stack (DB via Testcontainers, RabbitMQ, and the API via `WebApplicationFactory`).

## المراجع الحالية / Current References

- لا يوجد أي `ProjectReference` حالياً.
- No `ProjectReference` currently.

## المراجع المقترحة / Recommended References

| المشروع المرجع | السبب / Reason |
|---|---|
| **OpsPilot.API** | تشغيل التطبيق كامل (WebApplicationFactory) |
| **OpsPilot.Infrastructure** | الوصول للـ DbContext والخدمات الحقيقية |
| **OpsPilot.Application** | استخدام الـ Use Cases في السيناريوهات |

## الحزم / NuGet Packages

| الحزمة | الإصدار | ملاحظات |
|---|---|---|
| `coverlet.collector` | `6.0.4` | تغطية الكود |
| `Microsoft.NET.Test.Sdk` | `17.14.1` | تشغيل الـ tests |
| `xunit` | `2.9.3` | إطار الاختبارات |
| `xunit.runner.visualstudio` | `3.1.4` | تكامل الـ xunit مع الـ VS |

**أمثلة على حزم مقترحة مستقبلًا / Suggested future packages:** `Microsoft.AspNetCore.Mvc.Testing` (WebApplicationFactory), `Testcontainers` (تجهيز الـ DB)، `FluentAssertions`.

## الملاحظات إضافية / Additional Notes

- ملف المحتوى الحالي: `UnitTest1.cs` (قالب افتراضي فاضي)
- التارجت: `net10.0` — `IsPackable: false`