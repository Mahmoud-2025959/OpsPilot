# OpsPilot — التوثيق / Documentation

مشروع Clean Architecture مبني على **.NET 10** — فهرسة للمشاريع وتوزيع المراجع والحزم لكل Layer.

## المشاريع / Projects

| Layer | المشروع | الدور / Responsibility |
|---|---|---|
| API | `OpsPilot.API` | نقطة الدخول (Web API + Controllers + Middleware + Extensions) |
| Application | `OpsPilot.Application` | منطق الأعمال / Use Cases |
| Contracts | `OpsPilot.Contracts` | عقود الـ DTOs ورسائل التبادل |
| Domain | `OpsPilot.Domain` | الكيانات والدومين (أعمق طبقة) |
| Infrastructure | `OpsPilot.Infrastructure` | الاتصالات الخارجية (DB, Redis, RabbitMQ, Notifications) |
| Tests — Architecture | `OpsPilot.ArchitectureTests` | فرض قواعد العمارة (منع الدوائر في المراجع) |
| Tests — Integration | `OpsPilot.IntegrationTests` | اختبارات التكامل مع الخدمات الحقيقية |
| Tests — Unit | `OpsPilot.UnitTests` | اختبارات الوحدات لكل Use Case |

## مخطط المراجع (الموصى به) / Reference Graph

```
                        ┌──────────────────┐
                        │   OpsPilot.API    │ ───► OpsPilot.Application ───► OpsPilot.Domain
                        │                   │ ───► OpsPilot.Contracts
                        │                   │ ───► OpsPilot.Infrastructure ──► OpsPilot.Application
                        └──────────────────┘                               └─► OpsPilot.Domain
                                                                              └─► OpsPilot.Contracts
```

قاعدة ذهبية / Golden Rule: الاتجاه من الخارج للداخل فقط. **Domain و Contracts** لا يسترجعان أي مشروع.

## فهرس الملفات / Index

| الملف | المحتوى |
|---|---|
| [Domain.md](Domain.md) | `OpsPilot.Domain` |
| [Application.md](Application.md) | `OpsPilot.Application` |
| [Contracts.md](Contracts.md) | `OpsPilot.Contracts` |
| [Infrastructure.md](Infrastructure.md) | `OpsPilot.Infrastructure` |
| [API.md](API.md) | `OpsPilot.API` |
| [ArchitectureTests.md](ArchitectureTests.md) | `OpsPilot.ArchitectureTests` |
| [IntegrationTests.md](IntegrationTests.md) | `OpsPilot.IntegrationTests` |
| [UnitTests.md](UnitTests.md) | `OpsPilot.UnitTests` |