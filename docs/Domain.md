# OpsPilot.Domain

> المشروع: `OpsPilot.Domain` — الطبقة الأعمق في الـ Clean Architecture على `.NET 10`.

## نبذة / Overview

الطبقة الأساسية اللي بتحتوي كيانات الدومين (Entities) والقيم (ValueObjects) والـ Enums وأحداث الدومين (Domain Events) والاستثناءات المخصصة. مفيش فيها أي اعتماد على بنية تحتية أو حزم خارجية، وبتفضل **قابلة للنقل** (transportable) من غير أي تبعيات.

The innermost layer — contains domain entities, value objects, enums, domain events, and custom exceptions with zero external dependencies.

## المراجع الحالية / Current References

- لا يوجد أي `ProjectReference` حالياً.
- No `ProjectReference` currently.

## المراجع المقترحة / Recommended References

- **لا يسترجع أي مشروع** — أعمق طبقة، ممنوع تسترجع غيرها.
- **References nothing** — the deepest layer; it must not depend on any other project.

## الحزم / NuGet Packages

| الحزمة | الإصدار | ملاحظات |
|---|---|---|
| — | — | لا يوجد أي `PackageReference` حالياً. No packages currently. |

## المجلدات / Folders

| المجلد | الدور / Purpose |
|---|---|
| `Entities/` | الكيانات الأساسية للمشروع (مثل Incident, Service, Shift, Team, User...) — بتحتوي على الدومين هيكل وسلوك |
| `ValueObjects/` | القيم غير القابلة للتغيير واللي مش ليها هوية (مثل قبضة الداتا، المواعيد، الأكواد) — Immutable, identity-less values |
| `Enums/` | الـ enumerations الخاصة بمنطق الدومين |
| `Events/` | أحداث الدومين (Domain Events) اللي بتعدل عليها الـ Application |
| `Exceptions/` | استثناءات مخصصة خاصة بالدومين (مثل `NotFound`, `BusinessRuleViolation`) |

## ملاحظات إضافية / Additional Notes

- مساحة الأسماء: `OpsPilot.Domain`
- التارجت: `net10.0` مع `Nullable` و `ImplicitUsings` مفعّلين