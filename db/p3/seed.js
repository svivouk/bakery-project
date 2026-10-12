/*
   reviews_db — демонстраційні дані (Проєкт №3: MongoDB)
*/

db = db.getSiblingDB("reviews_db");

/* ---------- Ідентифікатори з інших контекстів ------------------------ */

const productId   = UUID("a0000000-0000-4000-8000-000000000001");   // Медовик (Каталог)
const croissantId = UUID("a0000000-0000-4000-8000-000000000003");   // Круасан класичний (Каталог)

const customer1 = UUID("CUST01");     // Олена Коваленко (Замовлення)
const customer2 = UUID("CUST02");     // Андрій Мельник (Замовлення)

const order1 = UUID("ORD01");
const order2 = UUID("ORD02");

const orderItemMedovyk1  = UUID("a0000000-0000-4000-8000-000000000001");  // замовлення №1, Медовик
const orderItemMedovyk2  = UUID("a0000000-0000-4000-8000-000000000001");  // замовлення №2, Медовик
const orderItemCroissant = UUID("a0000000-0000-4000-8000-000000000003");  // замовлення №1, Круасани

const moderator = UUID("EMP1");     // працівник пекарні

const review1Id = new ObjectId();
const review2Id = new ObjectId();

/* ---------- 1) Review: схвалений, з фото й відповіддю ---------------- */
db.reviews.insertOne({
  _id: review1Id,
  productId: productId,
  productName: "Медовик",                       // копія, оновлюється подією ProductRenamed
  customerId: customer1,
  customerDisplayName: "Олена К.",              // знімок на момент відгуку
  orderItemId: orderItemMedovyk1,
  rating: NumberInt(5),
  text: "Дуже ніжний, коржі м'які, не надто солодкий. Доставили вчасно.",
  status: "Approved",
  moderation: {                                  // embed: результат модерації, 1:1
    moderatedAt: new Date("2026-10-09T09:15:00Z"),
    moderatorId: moderator
  },
  photos: [                                      // embed: ReviewPhoto, до 5 штук
    { photoId: new ObjectId(), url: "placeholder",
      caption: "Розріз торта", uploadedAt: new Date("2026-10-08T18:02:00Z") },
    { photoId: new ObjectId(), url: "placeholder",
      uploadedAt: new Date("2026-10-08T18:02:00Z") }
  ],
  reply: {                                       // embed: ReviewReply, 0..1
    text: "Дякуємо за теплий відгук! Чекаємо на вас знову.",
    authorId: moderator,
    authorName: "Пекарня",
    createdAt: new Date("2026-10-09T10:00:00Z")
  },
  reportCount: NumberInt(1),
  createdAt: new Date("2026-10-08T18:02:00Z"),
  updatedAt: new Date("2026-10-09T10:00:00Z"),
  createdBy: "customer:CUST01",
  updatedBy: "staff:EMP1",
  isDeleted: false,
  version: NumberInt(3)            // 1 = створено, 2 = модерація, 3 = відповідь (скарга version не змінила)
});

/* ---------- 2) Review на модерації: без фото й відповіді ------------- */
db.reviews.insertOne({
  _id: review2Id,
  productId: productId,
  productName: "Медовик",
  customerId: customer2,
  customerDisplayName: "Андрій М.",
  orderItemId: orderItemMedovyk2,
  rating: NumberInt(2),
  text: "Торт приїхав із запізненням на годину.",
  status: "Pending",
  reportCount: NumberInt(0),
  createdAt: new Date("2026-10-10T08:30:00Z"),
  updatedAt: new Date("2026-10-10T08:30:00Z"),
  createdBy: "customer:CUST02",
  updatedBy: "customer:CUST02",
  isDeleted: false,
  version: NumberInt(1)
});

/* ---------- 3) ReviewReport: скарга на відгук №1 (reference) --------- */
db.reviewReports.insertOne({
  reviewId: review1Id,
  reporterCustomerId: customer2,
  reason: "FakeReview",
  comment: "Схоже на рекламний відгук",
  status: "Dismissed",
  createdAt: new Date("2026-10-09T11:20:00Z"),
  resolvedAt: new Date("2026-10-09T12:05:00Z"),
  resolvedBy: moderator,
  resolution: "Покупку підтверджено (orderItemId), скаргу відхилено.",
  createdBy: "customer:CUST02",
  updatedAt: new Date("2026-10-09T12:05:00Z"),
  updatedBy: "staff:EMP1",
  version: NumberInt(2)            // 1 = подано, 2 = розглянуто
});

/* ---------- 4) ProductRatingSummary: лише Approved, не видалені ------ */
// 12 відгуків, сума 52 → середня 4.33 (збігається з ProductRatings у db/p1/seed.sql)
db.productRatingSummaries.insertOne({
  _id: productId,
  count: NumberInt(12),
  sum: NumberInt(52),
  average: 4.33,
  distribution: { "1": NumberInt(0), "2": NumberInt(1), "3": NumberInt(1),
                  "4": NumberInt(3), "5": NumberInt(7) },
  version: NumberLong(12),                       // порядок подій, а не токен редагування
  updatedAt: new Date("2026-10-09T09:15:00Z")
});

/* ---------- 5) reviewEligibility: що клієнт може оцінити ------------- */
// Створюється подією OrderCompleted. Замовлення №3 ще не завершене, тому запису для нього немає.
db.reviewEligibility.insertMany([
  {   // використано: відгук №1 уже залишено
    customerId: customer1, productId: productId, orderItemId: orderItemMedovyk1,
    orderId: order1, eligibleSince: new Date("2026-10-07T16:40:00Z"), reviewId: review1Id
  },
  {   // використано: відгук №2 очікує модерації
    customerId: customer2, productId: productId, orderItemId: orderItemMedovyk2,
    orderId: order2, eligibleSince: new Date("2026-10-08T13:10:00Z"), reviewId: review2Id
  },
  {   // доступно: клієнт ще не оцінив круасани
    customerId: customer1, productId: croissantId, orderItemId: orderItemCroissant,
    orderId: order1, eligibleSince: new Date("2026-10-07T16:40:00Z"), reviewId: null
  }
]);

/* ---------- 6) outbox: подія для Каталогу (копія рейтингу) ----------- */
db.outbox.insertOne({
  eventId: UUID("9b4e7a1c-2d8f-4c63-a5e0-7d1b3f9c8a24"),
  eventType: "RatingSummaryChanged",
  aggregateId: productId,
  payload: { productId: productId, average: 4.33, count: 12, version: NumberLong(12) },
  createdAt: new Date("2026-10-09T09:15:01Z"),
  processedAt: null
});

/* ---------- 7) inbox: оброблена вхідна подія (TTL 30 днів) ----------- */
db.inbox.insertOne({
  _id: UUID("4f8c2e6a-1d9b-4a73-8e05-3c7b9d1f5a68"),   // = eventId
  eventType: "ProductRenamed",
  processedAt: new Date("2026-10-06T07:00:03Z")
});

/* ---------- Перевірка-------------------------------- */
// Відгуки товару, найновіші першими (використовує індекс {productId, status, createdAt}):
//   db.reviews.find({ productId, status: "Approved", isDeleted: false }).sort({ createdAt: -1 })
// Що клієнт може оцінити зараз:
//   db.reviewEligibility.find({ customerId: customer1, reviewId: null })
// Черга модерації:
//   db.reviews.find({ status: "Pending", isDeleted: false }).sort({ createdAt: 1 })
