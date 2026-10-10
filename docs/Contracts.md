# OpsPilot.Contracts

> المشروع: `OpsPilot.Contracts` — طبقة العقود في الـ Clean Architecture على `.NET 10`.

## نبذة / Overview

البتركز على عقود الـ DTOs ورسائل التبادل (Messages) بين الطبقات. بتضمّن هياكل البيانات اللي بتستقبلها الـ Controllers وبترجعها، ورسائل الحداث اللي بتتم عبر RabbitMQ — وده بيخلي التبادل بين الـ API والـ Application والـ Infrastructure معرّف بوضوح.

Contains DTOs and messaging contracts shared across layers so API / Application / Infrastructure exchange data through well-defined types.

## المراجع الحالية / Current References

- لا يوجد أي `ProjectReference` حالياً.
- No `ProjectReference` currently.

## المراجع المقترحة / Recommended References

- **لا يسترجع أي مشروع** — بتعتمد عليها Application و API و Infrastructure، فممنوع تسترجع غيره.
- **References nothing** — other layers depend on it; it must not depend on others.

## الحزم / NuGet Packages

| الحزمة | الإصدار | ملاحظات |
|---|---|---|
| — | — | لا يوجد أي `PackageReference` حالياً. No packages currently. |

## المحتوى / Contents

| العنصر | الوصف / Description |
|---|---|
| `DTOs / Requests` | الـ payloads اللي بتتبعت (إنشاء/تعديل/استعلام) |
| `DTOs / Responses` | الـ payloads اللي بترجع (نتائج الـ API) |
| Messaging contracts | رسائل الحداث الخاصة بالـ RabbitMQ |

## ملاحظات إضافية / Additional Notes

- مساحة الأسماء: `OpsPilot.Contracts`
- التارجت: `net10.0` مع `Nullable` و `ImplicitUsings` مفعّلين