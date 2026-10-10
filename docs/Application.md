# OpsPilot.Application

> المشروع: `OpsPilot.Application` — طبقة منطق الأعمال في الـ Clean Architecture على `.NET 10`.

## نبذة / Overview

الطبقة اللي بتحتوي على حالات الاستخدام (Use Cases) ومنطق الأعمال من غير أي اعتماد على البنية التحتية. بتعتمد على الـ Abstractions (واجهات) بدل التنفيذات المباشرة، والـ Use Cases بتتنجز هناك بعد إضافة الـ CQRS/MediatR والـ Behaviors على الـ pipelines.

Contains business logic / Use Cases and abstractions (interfaces); depends on abstractions, not concrete infrastructure.

## المراجع الحالية / Current References

- لا يوجد أي `ProjectReference` حالياً.
- No `ProjectReference` currently.

## المراجع المقترحة / Recommended References

| المشروع المرجع | السبب / Reason |
|---|---|
| **OpsPilot.Domain** | استخدام الكيانات والاستثناءات في منطق الأعمال |
| **OpsPilot.Contracts** | استخدام الـ DTOs داخل الـ Commands/Queries |

## الحزم / NuGet Packages

| الحزمة | الإصدار | ملاحظات |
|---|---|---|
| — | — | لا يوجد أي `PackageReference` حالياً. No packages currently.

**أمثلة على حزم مقترحة مستقبلًا / Suggested future packages:** `MediatR` (لـ CQRS), `FluentValidation` (لتحقق الـ Commands), `Microsoft.Extensions.Logging.Abstractions`.

## المجلدات / Folders

| المجلد | الدور / Purpose |
|---|---|
| `Abstractions/` | الواجهات (Interfaces) للـ Repositories والخدمات — عشان الـ Infrastructure تنفذها |
| `Behaviors/` | الحلقات الوسيطة على الـ MediatR pipeline (Validation, Logging, Transaction...) |
| `Common/` | أدوات وكلاسات مشتركة لي الـ Use Cases (مثل نتيجة العمليات، إعدادات الدومين) |
| `Features/Incidents/` | أوامر واستعلامات (Commands/Queries) حوادث التشغيل (Incidents) |
| `Features/Services/Teams/` | منطق الفِرق (Teams) جوه خدمات الدومين |
| `Features/Users/` | منطق الـ Users (إنشاء، تعديل، بحث...) |

## ملاحظات إضافية / Additional Notes

- مساحة الأسماء: `OpsPilot.Application`
- التارجت: `net10.0` مع `Nullable` و `ImplicitUsings` مفعّلين
- ممنوع استيراد `OpsPilot.Infrastructure` أو `OpsPilot.API` — This layer must never reference Infrastructure/API.