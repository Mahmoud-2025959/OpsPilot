# OpsPilot.ArchitectureTests

> المشروع: `OpsPilot.ArchitectureTests` — اختبارات العمارة في الـ Clean Architecture على `.NET 10`.

## نبذة / Overview

سلسلة الـ tests اللي بتفرض قواعد الـ Clean Architecture على مستوى المرجعي (Dependency Rules): ممنوع مثلاً `OpsPilot.Domain` يسترجع `OpsPilot.Infrastructure`، وممنوع للطبقات الداخلية تروح لطبقات أبعد. بيعمل دا عبر فحص الـ dependency graph اللي بتوصله من برج واضح لكل المشاريع.

Enforces Clean Architecture dependency rules (e.g., Domain must not reference Infrastructure) by scanning all projects' dependency graph.

## المراجع الحالية / Current References

- لا يوجد أي `ProjectReference` حالياً.
- No `ProjectReference` currently.

## المراجع المقترحة / Recommended References

| المشروع المرجع | السبب / Reason |
|---|---|
| **OpsPilot.API** | فحص قواعد العمارة لكل المشاريع |
| **OpsPilot.Application** | مشاركة في فحص الديفندنشي |
| **OpsPilot.Contracts** | مشاركة في فحص الديفندنشي |
| **OpsPilot.Domain** | مشاركة في فحص الديفندنشي |
| **OpsPilot.Infrastructure** | مشاركة في فحص الديفندنشي |

## الحزم / NuGet Packages

| الحزمة | الإصدار | ملاحظات |
|---|---|---|
| `coverlet.collector` | `6.0.4` | تغطية الكود |
| `Microsoft.NET.Test.Sdk` | `17.14.1` | تشغيل الـ tests |
| `xunit` | `2.9.3` | إطار الاختبارات |
| `xunit.runner.visualstudio` | `3.1.4` | تكامل الـ xunit مع الـ VS |

## الملاحظات إضافية / Additional Notes

- ملف المحتوى الحالي: `UnitTest1.cs` (قالب افتراضي فاضي — جاهز يُستبدل باختبارات العمارة)
- التارجت: `net10.0` — `IsPackable: false`