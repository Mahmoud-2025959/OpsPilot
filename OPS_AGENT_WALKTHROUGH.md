# OpsPilot Agent Walkthrough

هذا الدليل مخصص لـ Agent يعمل بعدك، عشان يعرف يبدأ من أول حاجة ويكمل كل الخطوات بالترتيب لحد ما يجهز البيئة كاملة. 

## 1) نظرة عامة

- **Base URL**: `http://localhost:5093` (افتراضى. لو مختلف غيره)
- **API Prefix**: `api/v1`
- **Auth**: 
  - معظم الـendpoints محتاجة `Authorization: Bearer {{accessToken}}`
  - `POST /api/v1/events` بيستخدم `X-API-Key` بدل Bearer، وبيحتاج `events.ingest` policy (مقيد بـ API Key)
- **Response Format**: كل حاجة بترجع `Response<T>` أو `PagedResponse<T>` مع `correlationId` غالبًا
- **Tenant**: بيتحدد من التوكن دايمًا، مفيش حاجة بتاخد `tenantId` من Body إلا فى حالات نادرة جداً وممنوع
- **Enums**: بيتكتبوا كـ string case-insensitive (`Severity`, `EnvironmentType`, `TeamRole`, `NotificationChannel`)

## 2) متغيرات لازم يحددها Agent

قبل ما يبدأ، يحط القيم دى:

| متغير | مطلوب | مثال | ملاحظات |
|---|---|---|---|
| `baseUrl` | نعم | `http://localhost:5093` | رابط الـAPI |
| `companyName` | نعم لخطوة Register | `Acme` | اسم الشركة |
| `companySlug` | اختياري | `acme` | لو مش محدد، السيستم هيولد slug لوحده |
| `ownerEmail` | نعم | `owner-acme@ops-pilot.test` | ايميل مالك الشركة |
| `ownerPassword` | نعم | `Sup3rSecret!` | لازم >= 8 حروف |
| `ownerDisplayName` | اختياري | `Acme Owner` | افتراضي هيبقى واضح |
| `accessToken` | بيتولد تلقائى بعد Login/Register | - | يحط فى Header |
| `refreshToken` | بيتولد تلقائى | - | للاستخدام فى refresh/logout |
| `tenantId` | بيتاخد من response | - | للمراجعة فقط، السيستم بيستخدمه من التوكن |

**نصيحة:** أفضل حاجة تبدأ بـ `register-tenant` عشان تعمل شركة جديدة + Owner + Roles + Permissions + SLA Policies تلقائياً.

## 3) ترتيب التنفيذ خطوة بخطوة

نفذ بالترتيب ده بالظبط. لو حاجه فشلت، شوف رسالة الخطأ ومعناها غالبًا واضح.

### الخطوة 1: إنشاء شركة + تسجيل دخول أول Owner
**الغرض:** عمل Tenant جديد مع Owner جاهز.

#### 1.1 `POST {{baseUrl}}/api/v1/auth/register-tenant` [AllowAnonymous]
**Body:**
```json
{
  "companyName": "{{companyName}}",
  "slug": "{{companySlug}}",
  "email": "{{ownerEmail}}",
  "displayName": "{{ownerDisplayName}}",
  "password": "{{ownerPassword}}",
  "confirmPassword": "{{ownerPassword}}"
}
```

**ملاحظات:**
- `slug` لو فاضى، السيستم هيشتق منه من `companyName`. لو كتبته لازم يكون lowercase alphanumeric مع `-` فقط ويبدأ بحرف/رقم
- `confirmPassword` لازم يطابق `password` بالظبط
- `password` >= 8

**مثال Response (نجاح):**
```json
{
  "succeeded": true,
  "errors": [],
  "data": {
    "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
    "tenantId": "11111111-2222-3333-4444-555555555555",
    "email": "owner-acme@ops-pilot.test",
    "displayName": "Acme Owner",
    "roles": ["Owner"],
    "permissions": ["..."],
    "accessToken": "eyJ...",
    "refreshToken": "...",
    "tokenExpiresAt": "2026-10-27T12:00:00+00:00",
    "refreshExpiresAt": "2026-11-03T11:45:00+00:00"
  },
  "correlationId": "..."
}
```

**Action:** خزن `data.accessToken` فى `{{accessToken}}` و`data.refreshToken` فى `{{refreshToken}}`. استخدم `Authorization: Bearer {{accessToken}}` لباقى الخطوات اللى محتاجة مصادقة.

> لو عندك Owner موجود بالفعل، تقدر تبدأ بـ **1.2 Login** بدل كده.

#### 1.2 `POST {{baseUrl}}/api/v1/auth/login` [AllowAnonymous]
**Body:**
```json
{
  "identifier": "{{ownerEmail}}",
  "password": "{{ownerPassword}}"
}
```

**Response:** نفس شكل `AuthResponseDto` كـ register-tenant. خزن التوكنات.

#### 1.3 `POST {{baseUrl}}/api/v1/auth/refresh` [AllowAnonymous] (اختياري للتأكد)
**Body:**
```json
{
  "refreshToken": "{{refreshToken}}"
}
```
**Response:** `RefreshTokenResponseDto` بيحتوى على `accessToken` جديد و`refreshToken` جديد و`expiresAt`. **مهم جداً:** كل refresh بيعمل single-use، لو استخدمت نفس الـrefreshToken تانى هتترفض وتلغى الـchain كله.

#### 1.4 `GET {{baseUrl}}/api/v1/users/me` [Auth]
**الغرض:** تأكد إن التوكن شغال وشوف بروفايلك + صلاحياتك.
**Response:** `CurrentUserDto` فيه `id`, `email`, `displayName`, `roles`, `permissions`, `tenantId`.

### الخطوة 2: فهم البيئة (Roles, Permissions, Tenant)
اعمل الخطوات دى عشان تعرف تقدر تعمل إيه.

#### 2.1 `GET {{baseUrl}}/api/v1/permissions` [Auth]
يطلع كل Permissions الموجودة مقسمة حسب Category. مقروء للجميع المسجلين دخول.

#### 2.2 `GET {{baseUrl}}/api/v1/roles` [Auth]
يطلع كل Roles فى الشركة مع Permissions بتاعتها. مقروء للجميع.

#### 2.3 `GET {{baseUrl}}/api/v1/tenant` [Auth - يحتاج `tenant.settings`]
يطلع بيانات الشركة. **ملاحظة:** لازم يكون معاك Permission ده (Owner بيحمله غالبًا).

لو عايز تغير اسم الشركة:
- `PUT {{baseUrl}}/api/v1/tenant` body: `{"name": "New Name"}`
- `POST {{baseUrl}}/api/v1/tenant/deactivate` / `POST {{baseUrl}}/api/v1/tenant/activate`

### الخطوة 3: Users & Teams
**محتاج صلاحية:** `users.manage`, `roles.change` لبعض الأكشنز.

#### 3.1 `GET {{baseUrl}}/api/v1/users` [Auth - `users.manage`] 
Query params: `search`, `role`, `page`, `pageSize`. بيرجع `PagedResponse<UserDto>`.

#### 3.2 `POST {{baseUrl}}/api/v1/users` [Auth - `users.manage`]
**Body:**
```json
{
  "email": "engineer@acme.test",
  "displayName": "Engineer One",
  "password": "Sup3rSecret!",
  "confirmPassword": "Sup3rSecret!",
  "roles": ["Engineer"]
}
```
**ملاحظة:** `tenantId` بيتاخد من التوكن تلقائياً، متكتبهوش فى Body.

احفظ `data` اللى هو الـ`userId` لو هتستخدمه بعد كده.

#### 3.3 إدارة صلاحيات المستخدم
- `GET {{baseUrl}}/api/v1/users/{id}/permissions` [Auth - `users.manage`] - بيظهر كل Permissions اللى بيملكها فعلاً (مجمعة من Roles)
- `PUT {{baseUrl}}/api/v1/users/{id}/roles` [Auth - `roles.change`] Body: `{"roles": ["Engineer", "IncidentResponder"]}`
- `PATCH {{baseUrl}}/api/v1/users/{id}/status` [Auth - `users.manage`] Body: `{"isActive": false}` (لو بيدخل وتفعيل يبقى `true`)

#### 3.4 Teams
- `GET {{baseUrl}}/api/v1/teams` [Auth] Query: `page`, `pageSize`
- `POST {{baseUrl}}/api/v1/teams` [Auth - `teams.manage`] Body: `{"name": "Platform", "description": "..."}`
- `POST {{baseUrl}}/api/v1/teams/{id}/members` [Auth - `teams.manage`] Body: `{"userId": "GUID", "role": "Lead"}` (role ممكن يبقى `Member` أو `Lead`، لو فاضى هيبقى `Member`)
- `DELETE {{baseUrl}}/api/v1/teams/{id}/members/{userId}` [Auth - `teams.manage`]

### الخطوة 4: Services & SLA Policies
#### 4.1 Services
- `GET {{baseUrl}}/api/v1/services` [Auth]
- `GET {{baseUrl}}/api/v1/services/{id}` [Auth]
- `POST {{baseUrl}}/api/v1/services` [Auth - `services.manage`] 
**Body:**
```json
{
  "name": "API Gateway",
  "environment": "Production",
  "slug": "api-gateway",
  "description": "Main gateway",
  "repositoryUrl": "https://github.com/acme/api-gateway",
  "ownerTeamId": "GUID-OPTIONAL"
}
```
`environment` يقدر يبقى: `Production`, `Staging`, `Development`

- `PUT {{baseUrl}}/api/v1/services/{id}` [Auth - `services.manage`] نفس شكل Create لكن بدون `tenantId`

احفظ `serviceId` عشان تستخدمه فى Incidents بعد كده.

#### 4.2 SLA Policies
- `GET {{baseUrl}}/api/v1/sla-policies` [Auth - `sla.manage`]
- `PUT {{baseUrl}}/api/v1/sla-policies` [Auth - `sla.manage`]
**Body:**
```json
{
  "policies": [
    { "severity": "P0", "responseMinutes": 15, "resolutionMinutes": 60 },
    { "severity": "P1", "responseMinutes": 30, "resolutionMinutes": 120 },
    { "severity": "P2", "responseMinutes": 60, "resolutionMinutes": 240 },
    { "severity": "P3", "responseMinutes": 120, "resolutionMinutes": 480 },
    { "severity": "P4", "responseMinutes": 240, "resolutionMinutes": 1440 }
  ]
}
```
`severity` يقدر يبقى: `P0`, `P1`, `P2`, `P3`, `P4`

### الخطوة 5: Incidents (أهم حاجة)
كل حاجة هنا محتاجة صلاحيات معينة حسب الإجراء.

#### 5.1 قراءة Incidents
- `GET {{baseUrl}}/api/v1/incidents` [Auth - `incidents.read`]
Query: `status`, `severity`, `serviceId`, `assignedTo`, `from`, `to`, `page`, `pageSize`

- `GET {{baseUrl}}/api/v1/incidents/{id}` [Auth - `incidents.read`]
- `GET {{baseUrl}}/api/v1/incidents/{id}/timeline` [Auth - `incidents.read`]
- `GET {{baseUrl}}/api/v1/incidents/{id}/comments` [Auth - `incidents.read`]

#### 5.2 إنشاء Incident
`POST {{baseUrl}}/api/v1/incidents` [Auth - `incidents.create`]
**Body:**
```json
{
  "serviceId": "{{serviceId}}",
  "title": "Database connection pool exhausted",
  "description": "Users getting timeouts on login",
  "severity": "P1",
  "correlationKey": "deploy-2026-10-27-01"
}
```
**ملاحظات:**
- `tenantId` و`reportedBy` بيتاخدوا من التوكن
- `severity`: `P0`-`P4`
- `correlationKey` اختيارى

احفظ `data` اللى هو `incidentId`.

#### 5.3 تحديث / تعيين
- `PUT {{baseUrl}}/api/v1/incidents/{id}` [Auth - `incidents.update`] Body: `{"title": "...", "description": "...", "severity": "P0"}`
- `POST {{baseUrl}}/api/v1/incidents/{id}/assign` [Auth - `incidents.assign`] Body: `{"assigneeId": "{{userId}}"}` (الـ`assignedBy` بيتاخد من التوكن)

#### 5.4 التنقل بين الحالات (Transitions) - **مهم جداً**
`POST {{baseUrl}}/api/v1/incidents/{id}/transitions` [Auth - **حسب الحالة المستهدفة**]

**قاعدة الصلاحيات حسب `toStatus`:**
| toStatus | Permission المطلوب |
|---|---|
| `Open` | `incidents.update` |
| `Acknowledged` | `incidents.update` |
| `Investigating` | `incidents.update` |
| `Mitigated` | `incidents.update` |
| `Resolved` | `incidents.resolve` |
| `Closed` | `incidents.close` |

**Body:**
```json
{
  "toStatus": "Acknowledged",
  "note": "On it"
}
```

**ترتيب نموذجى صحيح:**
1. `Open` (بعد الإنشاء غالبًا بيبدأ كده)
2. `Acknowledged` (needs `incidents.update`)
3. `Investigating` (needs `incidents.update`)
4. `Mitigated` (needs `incidents.update`)
5. `Resolved` (needs `incidents.resolve`)
6. `Closed` (needs `incidents.close`) - أو تقدر تـ`Open` تانى لو احتجت (`incidents.update`)

#### 5.5 Comments
- `POST {{baseUrl}}/api/v1/incidents/{id}/comments` [Auth - `incidents.comment`] Body: `{"body": "Investigating logs..."}`

### الخطوة 6: Postmortems
**قاعدة ذهبية:** مفيش Postmortem إلا للـIncident اللى اتحط فى سياق صح. كمان **مش ممكن تنشر Postmortem إلا لو Incident يبقى `Resolved` أو `Closed`**.

**Routes موجودة فى اتنين أماكن:**
- `/api/v1/incidents/{incidentId}/postmortem` للـcreate/read/update
- `/api/v1/postmortems/...` للـlist/get/review/return/publish

#### 6.1 فتح Postmortem
`POST {{baseUrl}}/api/v1/incidents/{incidentId}/postmortem` [Auth - `postmortems.write`]
**Body:**
```json
{
  "title": "Postmortem: DB pool exhaustion",
  "summary": "Short summary",
  "rootCause": "Missing connection leak fix",
  "actionItems": [
    { "title": "Add connection timeout", "ownerUserId": "{{userId}}", "dueDate": "2026-11-01T00:00:00+00:00" }
  ]
}
```
احفظ `data.id` كـ `postmortemId`.

#### 6.2 تعديل Draft
`PUT {{baseUrl}}/api/v1/incidents/{incidentId}/postmortem` [Auth - `postmortems.write`] نفس Body تقريبًا

#### 6.3 سير المراجعة والنشر
- `POST {{baseUrl}}/api/v1/postmortems/{id}/review` [Auth - `postmortems.write`] يروح `InReview`
- `POST {{baseUrl}}/api/v1/postmortems/{id}/return-to-draft` [Auth - `postmortems.write`] يرجع `Draft` لو مرفوض
- `POST {{baseUrl}}/api/v1/postmortems/{id}/publish` [Auth - `postmortems.publish`] **يحتاج Incident يكون `Resolved` أو `Closed`**

#### 6.4 قراءة
- `GET {{baseUrl}}/api/v1/postmortems` [Auth - `postmortems.read`] Query: `status`, `incidentId`, `page`, `pageSize`
- `GET {{baseUrl}}/api/v1/postmortems/{id}` [Auth - `postmortems.read`]
- `GET {{baseUrl}}/api/v1/incidents/{incidentId}/postmortem` [Auth - `postmortems.read`]

### الخطوة 7: API Keys (لـ Events Ingestion)
**محتاج `apikeys.manage`**. `GET /api/v1/api-keys` بيعرض الـsecret كمان (مقصود عشان لو ضاع يقدر يقراه تانى).

#### 7.1 إنشاء API Key
`POST {{baseUrl}}/api/v1/api-keys` [Auth - `apikeys.manage`]
**Body:**
```json
{
  "name": "ingestion-key",
  "expiresAt": "2027-10-27T00:00:00+00:00"
}
```
`expiresAt` اختيارى. احفظ `data.key` أو `data.secret` حسب Response (فيه `CreatedApiKeyDto`).

#### 7.2 إدارة
- `GET {{baseUrl}}/api/v1/api-keys` [Auth - `apikeys.manage`]
- `GET {{baseUrl}}/api/v1/api-keys/{id}` [Auth - `apikeys.manage`]
- `POST {{baseUrl}}/api/v1/api-keys/{id}/rotate` [Auth - `apikeys.manage`] بيرجع secret جديد
- `DELETE {{baseUrl}}/api/v1/api-keys/{id}` [Auth - `apikeys.manage`] revoke

### الخطوة 8: Events Ingestion (بالـ API Key)
**أهم حاجة:** ده **مختلف عن الباقى**. بيستخدم `X-API-Key: {{apiKey}}` **مش** Bearer Token.

كمان عليه Rate Limit وبيحتاج `events.ingest` policy.

`POST {{baseUrl}}/api/v1/events` [Headers: `X-API-Key: {{apiKey}}`]
**Body:**
```json
{
  "service": "api-gateway",
  "environment": "Production",
  "severity": "P1",
  "message": "High latency detected",
  "timestamp": "2026-10-27T12:34:56+00:00"
}
```
**ملاحظات:**
- `tenantId` بيتاخد من الـAPI Key
- `service` بيقارن بالـslug/name بتاع Service (حسب Implementation)
- `environment`: `Production`, `Staging`, `Development`
- `severity`: `P0`-`P4`
- `timestamp` اختيارى غالبًا لكن يفضل تحطها
- بيرجع **202 Accepted** لو نجح

لو حدث ده عمل Incident تلقائى حسب قواعد النظام (حسب ما موجود فى Integration Tests).

### الخطوة 9: Webhooks
**محتاج `webhooks.read` / `webhooks.manage`**

- `GET {{baseUrl}}/api/v1/webhooks` [Auth - `webhooks.read`]
- `POST {{baseUrl}}/api/v1/webhooks` [Auth - `webhooks.manage`] Body: `{"url": "https://example.com/webhook"}`
  - **بيترجع `secret` مرة واحدة هنا فقط** - احفظه عشان تتحقق من التوقيع
- `PUT {{baseUrl}}/api/v1/webhooks/{id}` [Auth - `webhooks.manage`] Body: `{"url": "...", "isActive": true}`
- `POST {{baseUrl}}/api/v1/webhooks/{id}/rotate-secret` [Auth - `webhooks.manage`] بيطلع secret جديد
- `DELETE {{baseUrl}}/api/v1/webhooks/{id}` [Auth - `webhooks.manage`]
- `GET {{baseUrl}}/api/v1/webhooks/{id}/deliveries` [Auth - `webhooks.read`]
- `GET {{baseUrl}}/api/v1/webhooks/deliveries` [Auth - `webhooks.read`] Query: `endpointId`, `page`, `pageSize`

### الخطوة 10: Notifications, Preferences, Analytics, Audit
- `GET {{baseUrl}}/api/v1/notifications` [Auth] Query: `isRead`, `page`, `pageSize` (يبقى للمستخدم اللى مسجل دخول بس)
- `PUT {{baseUrl}}/api/v1/notification-preferences` [Auth] Body:
```json
{
  "preferences": [
    { "severity": "P0", "channel": "Email", "enabled": true },
    { "severity": "P1", "channel": "Email", "enabled": true },
    { "severity": "P0", "channel": "InApp", "enabled": true }
  ]
}
```
`channel`: `Email`, `InApp`, `Sms`, `Webhook` (حسب Enum)
**ملاحظات:** مفيش duplicates لنفس `(severity, channel)`، كل حاجة لازم تكون موجودة بشكل صحيح
- `GET {{baseUrl}}/api/v1/analytics/summary` [Auth - `analytics.read`] Query: `from`, `to`
- `GET {{baseUrl}}/api/v1/audit-logs` [Auth - `audit.read`] Query: `action`, `entityType`, `entityId`, `actorUserId`, `from`, `to`, `page`, `pageSize`

### الخطوة 11: تغيير الباسورد / تسجيل الخروج
- `POST {{baseUrl}}/api/v1/auth/change-password` [Auth]
```json
{
  "currentPassword": "{{ownerPassword}}",
  "newPassword": "NewSup3rSecret!",
  "confirmNewPassword": "NewSup3rSecret!"
}
```
- `POST {{baseUrl}}/api/v1/auth/logout` [Auth]
```json
{
  "refreshToken": "{{refreshToken}}"
}
```
**ملاحظة:** لو الrefreshToken مش موجود أو غريب بيرجع 401

## 4) نصائح للـAgent عشان يكمل لو حاجه وقعت

- **استخدم CorrelationId**: كل Response فيه `correlationId` لو عايز تتبع
- **Tenant Resolution**: مفيش داعى تحط `tenantId` فى أى Body، التوكن هو اللى بيحدد
- **Permissions**: لو شاف 403، شوف الـPermissions بتاعة الUser دلوقتى بـ `/users/me` أو `/users/{id}/permissions`
- **Transitions**: افحص صلاحية اللى هتحرك الحالة بناءً على `toStatus` بالظبط
- **Postmortem**: **ماينفعش تنشر غير لما Incident يبقى Resolved/Closed**
- **API Keys**: للـ`/events` **استخدم `X-API-Key` مش Bearer**
- **Refresh Tokens**: single-use، لو عايز تعمل session جديد اعمل refresh مرة واحدة
- **Enums**: حطهم Capitalized زى الموجودين (`P1`, `Production`, `Email`) أو حاول case-insensitive
- **Paged Responses**: `GET` اللى بيرجع قائمة غالبًا بيبقى `PagedResponse<T>` (مش `Response<List<T>>`)
- **Validation**: لو شاف 400 شوف `errors[]` عشان يعرف ايه اللى غلط بالظبط

## 5) قائمة تحقق سريعة

- [ ] عملت `register-tenant` أو `login` وخزنت `accessToken`
- [ ] عملت `users/me` وتأكدت من `roles` و`permissions`
- [ ] عملت `services` و`teams` لو محتاجين
- [ ] عملت `incidents` بالترتيب `create -> transition` مع صلاحيات صحيحة
- [ ] عملت `postmortem` لو عايز (لو الـincident بقى Resolved/Closed)
- [ ] عملت `api-key` + جربت `events` بـ `X-API-Key`
- [ ] عملت `webhooks` لو عايز
- [ ] راجعت `analytics/summary` و`audit-logs`
