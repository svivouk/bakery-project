# Приклади документів MongoDB (`reviews_db`)

Приклади відповідають `db/p3/seed.js` і покривають усі сутності контексту «Відгуки». Нотація — як у `mongosh` (`UUID(...)`, `ObjectId(...)`, `ISODate(...)`, `NumberInt(...)`).

Ідентифікатори збігаються з SQL-seed: `productId` — із `db/p1/seed.sql` («Медовик»), `customerId` та `orderItemId` — із `db/p2/seed.sql`.

## Embed чи reference

| Дані | Рішення | Обґрунтування |
|---|---|---|
| Фото відгуку (`photos[]`) | **embed**, до 5 | Завжди потрібні разом із відгуком; кількість обмежена валідатором (`maxItems: 5`) |
| Відповідь пекарні (`reply`) | **embed**, 0..1 | Одна відповідь, змінюється разом із відгуком |
| Модерація (`moderation`) | **embed**, 1:1 | Результат модерації цього відгуку |
| Скарги | **reference** (`reviewReports`) | Кількість необмежена, власний процес розгляду; у відгуку лише лічильник `reportCount` |
| Рейтинг товару | окрема колекція | Похідні дані, часто оновлюються, не повинні блокувати документ відгуку |

Якби відповідей пекарні могло бути багато (довге обговорення), `reply` довелося б винести в окрему колекцію за `reviewId`: необмежений масив розтягував би документ і наближав його до ліміту 16 МБ.

---

## 1. `reviews` — Review (+ ReviewPhoto, ReviewReply, moderation)

Схвалений відгук із двома фото й відповіддю пекарні:

```javascript
{
  _id: ObjectId("..."),
  productId: UUID("b0000000-0000-4000-8000-000000000001"),
  productName: "Медовик",                          // копія, оновлюється подією ProductRenamed
  customerId: UUID("e0000000-0000-4000-8000-000000000001"),
  customerDisplayName: "Олена К.",                 // знімок на момент відгуку
  orderItemId: UUID("f1000000-0000-4000-8000-000000000001"),   // унікальний: один відгук на покупку
  rating: 5,
  text: "Дуже ніжний, коржі м'які, не надто солодкий. Доставили вчасно.",
  status: "Approved",                              // Pending | Approved | Rejected
  moderation: {
    moderatedAt: ISODate("2026-10-09T09:15:00Z"),
    moderatorId: UUID("a1000000-0000-4000-8000-000000000001")
  },
  photos: [
    { photoId: ObjectId("..."), url: "https://cdn.example.com/reviews/medovik-1.jpg",
      caption: "Розріз торта", uploadedAt: ISODate("2026-10-08T18:02:00Z") },
    { photoId: ObjectId("..."), url: "https://cdn.example.com/reviews/medovik-2.jpg",
      uploadedAt: ISODate("2026-10-08T18:02:00Z") }
  ],
  reply: {
    text: "Дякуємо за теплий відгук! Чекаємо на вас знову.",
    authorId: UUID("a1000000-0000-4000-8000-000000000001"),
    authorName: "Пекарня",
    createdAt: ISODate("2026-10-09T10:00:00Z")
  },
  reportCount: 1,
  createdAt: ISODate("2026-10-08T18:02:00Z"),
  updatedAt: ISODate("2026-10-09T10:00:00Z"),
  createdBy: "customer:e0000000-0000-4000-8000-000000000001",
  updatedBy: "staff:a1000000-0000-4000-8000-000000000001",
  isDeleted: false,
  version: 3                                       // 1 створено, 2 модерація, 3 відповідь
}
```

Відгук на модерації (без фото, відповіді та `moderation`):

```javascript
{
  _id: ObjectId("..."),
  productId: UUID("b0000000-0000-4000-8000-000000000001"),
  productName: "Медовик",
  customerId: UUID("e0000000-0000-4000-8000-000000000002"),
  customerDisplayName: "Андрій М.",
  orderItemId: UUID("f1000000-0000-4000-8000-000000000002"),
  rating: 2,
  text: "Торт приїхав із запізненням на годину.",
  status: "Pending",
  reportCount: 0,
  createdAt: ISODate("2026-10-10T08:30:00Z"),
  updatedAt: ISODate("2026-10-10T08:30:00Z"),
  createdBy: "customer:e0000000-0000-4000-8000-000000000002",
  updatedBy: "customer:e0000000-0000-4000-8000-000000000002",
  isDeleted: false,
  version: 1
}
```

Валідатор вимагає: якщо `status` не `Pending`, поле `moderation` обов'язкове.

## 2. `reviewReports` — ReviewReport

```javascript
{
  _id: ObjectId("..."),
  reviewId: ObjectId("..."),                       // reference на reviews._id
  reporterCustomerId: UUID("e0000000-0000-4000-8000-000000000002"),
  reason: "FakeReview",                            // Spam | Offensive | FakeReview | Other
  comment: "Схоже на рекламний відгук",
  status: "Dismissed",                             // Open | Resolved | Dismissed
  createdAt: ISODate("2026-10-09T11:20:00Z"),
  resolvedAt: ISODate("2026-10-09T12:05:00Z"),
  resolvedBy: UUID("a1000000-0000-4000-8000-000000000001"),
  resolution: "Покупку підтверджено (orderItemId), скаргу відхилено.",
  createdBy: "customer:e0000000-0000-4000-8000-000000000002",
  updatedAt: ISODate("2026-10-09T12:05:00Z"),
  updatedBy: "staff:a1000000-0000-4000-8000-000000000001",
  version: 2                                       // 1 подано, 2 розглянуто
}
```

Унікальний індекс `{reviewId, reporterCustomerId}`: один клієнт скаржиться на відгук один раз. Скарги не видаляються: це матеріал модерації.

## 3. `productRatingSummaries` — ProductRatingSummary

```javascript
{
  _id: UUID("b0000000-0000-4000-8000-000000000001"),   // = productId
  count: 12,
  sum: 52,
  average: 4.33,
  distribution: { "1": 0, "2": 1, "3": 1, "4": 3, "5": 7 },
  version: NumberLong(12),                         // порядок подій, а не токен редагування
  updatedAt: ISODate("2026-10-09T09:15:00Z")
}
```

Рахується лише з відгуків `{status: "Approved", isDeleted: false}`. Після кожної зміни в outbox потрапляє подія `RatingSummaryChanged`, за якою Каталог оновлює свою копію (`ProductRatings`).

## 4. `reviewEligibility` — що клієнт може оцінити

```javascript
{
  _id: ObjectId("..."),
  customerId: UUID("e0000000-0000-4000-8000-000000000001"),
  productId: UUID("b0000000-0000-4000-8000-000000000003"),     // Круасан класичний
  orderItemId: UUID("f1000000-0000-4000-8000-000000000003"),   // унікальний
  orderId: UUID("f0000000-0000-4000-8000-000000000001"),
  eligibleSince: ISODate("2026-10-07T16:40:00Z"),
  reviewId: null                                   // стане ObjectId, коли відгук буде залишено
}
```

Створюється подією `OrderCompleted`. Відгук можливий лише за наявності запису з `reviewId: null`.

## 5. `outbox` і `inbox`

```javascript
// outbox: подія для Каталогу
{
  _id: ObjectId("..."),
  eventId: UUID("9b4e7a1c-2d8f-4c63-a5e0-7d1b3f9c8a24"),
  eventType: "RatingSummaryChanged",
  aggregateId: UUID("b0000000-0000-4000-8000-000000000001"),
  payload: { productId: UUID("b0000000-0000-4000-8000-000000000001"),
             average: 4.33, count: 12, version: NumberLong(12) },
  createdAt: ISODate("2026-10-09T09:15:01Z"),
  processedAt: null
}

// inbox: оброблена вхідна подія (TTL-індекс видаляє через 30 днів)
{
  _id: UUID("4f8c2e6a-1d9b-4a73-8e05-3c7b9d1f5a68"),   // = eventId; повторна подія дасть duplicate key
  eventType: "ProductRenamed",
  processedAt: ISODate("2026-10-06T07:00:03Z")
}
```

---

## Типові операції

**Оптимістична конкурентність** — модератор схвалює відгук, який прочитав із `version: 1`:

```javascript
const r = db.reviews.updateOne(
  { _id: review2Id, version: 1, isDeleted: false },
  { $set: { status: "Approved",
            moderation: { moderatedAt: new Date(), moderatorId: moderator },
            updatedAt: new Date(), updatedBy: "staff:a1000000-0000-4000-8000-000000000001" },
    $inc: { version: 1 } });
if (r.matchedCount === 0) { /* хтось уже змінив відгук: перечитати й показати конфлікт (HTTP 409) */ }
```

`version` збільшують лише змістовні зміни (текст, статус, модерація, відповідь). Лічильник `reportCount` змінюється через `$inc` **без** підвищення `version`, щоб скарга не інвалідувала роботу модератора.

**М'яке видалення:** те саме оновлення з `$set: { isDeleted: true, ... }` і `$inc: { version: 1 }`. Після цього рейтинг товару перераховується.

**Оновлення копії назви товару** за подією `ProductRenamed`:

```javascript
db.reviews.updateMany({ productId }, { $set: { productName: "Медовик класичний" } })
```

**Скарга:** вставити документ у `reviewReports` і збільшити лічильник у відгуку:

```javascript
db.reviews.updateOne({ _id: review1Id }, { $inc: { reportCount: 1 } })
```

**Повний перерахунок рейтингу товару:**

```javascript
db.reviews.aggregate([
  { $match: { productId, status: "Approved", isDeleted: false } },
  { $group: { _id: "$productId", count: { $sum: 1 }, sum: { $sum: "$rating" },
              average: { $avg: "$rating" } } }
])
```

## Індекси

| Колекція | Індекс | Для чого |
|---|---|---|
| `reviews` | `{orderItemId: 1}` унікальний | Один відгук на покупку (глобально, зокрема серед видалених) |
| `reviews` | `{productId: 1, status: 1, createdAt: -1}`, часткові за `isDeleted: false` | Відгуки товару: рівність, рівність, сортування |
| `reviews` | `{customerId: 1, createdAt: -1}`, часткові за `isDeleted: false` | Відгуки клієнта |
| `reviews` | `{createdAt: 1}`, часткові за `status: "Pending", isDeleted: false` | Черга модерації |
| `reviewReports` | `{reviewId: 1, reporterCustomerId: 1}` унікальний | Одна скарга від клієнта; префікс `reviewId` обслуговує «скарги на відгук» |
| `reviewReports` | `{createdAt: 1}`, часткові за `status: "Open"` | Черга скарг на розгляд |
| `reviewEligibility` | `{orderItemId: 1}` унікальний; `{customerId: 1, reviewId: 1}` | Перевірка права на відгук; «що клієнт може оцінити» |
| `outbox` | `{eventId: 1}` унікальний; `{createdAt: 1}` часткові за `processedAt: {$type: "null"}` (порівняння з `null` у частковому індексі не підтримується) | Публікація подій |
| `inbox` | `{processedAt: 1}` з TTL 30 днів | Ідемпотентність, автоочищення |
