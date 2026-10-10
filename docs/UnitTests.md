# OpsPilot.UnitTests

> المشروع: `OpsPilot.UnitTests` — اختبارات الوحدات في الـ Clean Architecture على `.NET 10`.

## نبذة / Overview

اختبارات الوحدات لكل Use Case و Handler وشبكة منطق الأعمال في الـ `OpsPilot.Application` و `OpsPilot.Domain` بتعزيل أي اعتماديات خارجية (Moq/NSubstitute/FakeItEasy) عشان التحقق من السلوك في عزلة.

Unit tests for business logic in Application/Domain with mocked external dependencies.

## المراجع الحالية / Current References

- لا يوجد أي `ProjectReference` حالياً.
- No `ProjectReference` currently.

## المراجع المقترحة / Recommended References

| المشروع المرجع | السبب / Reason |
|---|---|
| **OpsPilot.Application** | اختبار الـ Handlers و Behaviors |
| **OpsPilot.Domain** | اختبار الكيانات والـ ValueObjects |
| **OpsPilot.Contracts** | استخدام الـ DTOs في الاختبارات |

## الحزم / NuGet Packages

| الحزمة | الإصدار | ملاحظات |
|---|---|---|
| `coverlet.collector` | `6.0.4` | تغطية الكود |
| `Microsoft.NET.Test.Sdk` | `17.14.1` | تشغيل الـ tests |
| `xunit` | `2.9.3` | إطار الاختبارات |
| `xunit.runner.visualstudio` | `3.1.4` | تكامل الـ xunit مع الـ VS |

**أمثلة على حزم مقترحة مستقبلًا / Suggested future packages:** `Moq` (أو `NSubstitute`) للـ Mocking، `FluentAssertions` لتسهيل الـ Assertions.

## الملاحظات إضافية / Additional Notes

- ملف المحتوى الحالي: `UnitTest1.cs` (قالب افتراضي فاضي)
- التارجت: `net10.0` — `IsPackable: false`