# OpsPilot.Application

> المشروع: `OpsPilot.Application` — طبقة منطق الأعمال في الـ Clean Architecture على `.NET 10`.

## نبذة / Overview

الطبقة اللي بتحتوي على حالات الاستخدام (Use Cases) ومنطق الأعمال من غير أي اعتماد على البنية التحتية. بتعتمد على الـ Abstractions (واجهات) بدل التنفيذات المباشرة، والـ Use Cases بتتنجز هناك بعد إضافة الـ CQRS/MediatR والـ Behaviors على الـ pipelines.

Contains business logic / Use Cases and abstractions (interfaces); depends on abstractions, not concrete infrastructure.

## المراجع الحالية / Current References

| المشروع المرجع | السبب / Reason |
|---|---|
| **OpsPilot.Domain** | استخدام الكيانات والاستثناءات في منطق الأعمال |
| **OpsPilot.Contracts** | استخدام الـ DTOs داخل الـ Commands/Queries |

## الحزم / NuGet Packages

| الحزمة | الإصدار | ملاحظات |
|---|---|---|
| `MediatR` | `14.2.0` | الـ CQRS (Commands/Queries والـ handlers) |
| `FluentValidation` + `.DependencyInjectionExtensions` | `12.1.1` | التحقق من الـ Commands |
| `Microsoft.Extensions.DependencyInjection` | `10.0.12` | تسجيل الـ handlers |
| `Microsoft.Extensions.Logging.Abstractions` | `10.0.12` | واجهات التسجيل |
| `System.Security.Cryptography.Pkcs` | `9.0.21` | تبعية |

**ملاحظة:** الطبقة بتعتمد على الـ Abstractions (الواجهات) بس، والتنفيذات الحقيقية بتيجي من `OpsPilot.Infrastructure` عن طريق حقن التبعيات.

## المجلدات / Folders

| المجلد | الدور / Purpose |
|---|---|
| `Abstractions/` | الواجهات (Interfaces) للـ Repositories والخدمات — عشان الـ Infrastructure تنفذها |
| `Behaviors/` | الحلقات الوسيطة على الـ MediatR pipeline (Validation, Logging, Transaction...) |
| `Common/` | أدوات وكلاسات مشتركة لي الـ Use Cases (مثل نتيجة العمليات، إعدادات الدومين) |
| `Features/Incidents/` | أوامر واستعلامات (Commands/Queries) حوادث التشغيل (Incidents) |
| `Features/Services/Teams/` | منطق الفِرق (Teams) جوه خدمات الدومين |
| `Features/Users/` | منطق الـ Users (إنشاء، تعديل، بحث...) |
| `Features/Roles/` | منطق الأدوار والصلاحيات (Roles/Permissions) — إنشاء دور مخصص، تعديل صلاحياته، وقاعدة "من ما تملكش ما تديه"، وقاعدة إن دور الـ Owner ميتوزّعش غير من Owner |
| `Features/Permissions/` | قراءة كتالوج الصلاحيات العام (Permissions catalogue) |

## ملاحظات إضافية / Additional Notes

- مساحة الأسماء: `OpsPilot.Application`
- التارجت: `net10.0` مع `Nullable` و `ImplicitUsings` مفعّلين
- ممنوع استيراد `OpsPilot.Infrastructure` أو `OpsPilot.API` — This layer must never reference Infrastructure/API.