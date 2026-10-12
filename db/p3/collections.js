/* 
   reviews_db — Контекст «Відгуки» (Проєкт №3: MongoDB)
   Запуск: mongosh "mongodb://localhost:27017" collections.js, потім seed.js

 */

db = db.getSiblingDB("reviews_db");

/* ---------- Колекції та валідація ------------------------------------ */

db.createCollection("reviews", {
  validator: { $and: [ { $jsonSchema: {
    bsonType: "object",
    required: ["productId", "productName", "customerId", "customerDisplayName",
               "orderItemId", "rating", "status", "reportCount", "createdAt", "createdBy",
               "updatedAt", "updatedBy", "isDeleted", "version"],
    properties: {
      productId:           { bsonType: "binData" },
      productName:         { bsonType: "string", maxLength: 200 },   // копія, оновлюється ProductRenamed
      customerId:          { bsonType: "binData" },
      customerDisplayName: { bsonType: "string", maxLength: 100 },   // знімок
      orderItemId:         { bsonType: "binData" },
      rating:              { bsonType: "int", minimum: 1, maximum: 5 },
      text:                { bsonType: "string", maxLength: 2000 },
      status:              { enum: ["Pending", "Approved", "Rejected"] },
      moderation: {
        bsonType: "object",
        required: ["moderatedAt", "moderatorId"],
        properties: {
          moderatedAt: { bsonType: "date" },
          moderatorId: { bsonType: "binData" },
          reason:      { bsonType: "string", maxLength: 500 }
        }
      },
      photos: {
        bsonType: "array", maxItems: 5,
        items: {
          bsonType: "object",
          required: ["photoId", "url", "uploadedAt"],
          properties: {
            photoId:    { bsonType: "objectId" },
            url:        { bsonType: "string" },
            caption:    { bsonType: "string", maxLength: 200 },
            uploadedAt: { bsonType: "date" }
          }
        }
      },
      reply: {
        bsonType: "object",
        required: ["text", "authorId", "createdAt"],
        properties: {
          text:      { bsonType: "string", maxLength: 1000 },
          authorId:  { bsonType: "binData" },
          authorName:{ bsonType: "string" },
          createdAt: { bsonType: "date" },
          updatedAt: { bsonType: "date" }
        }
      },
      reportCount: { bsonType: "int", minimum: 0 },
      createdAt:   { bsonType: "date" },
      updatedAt:   { bsonType: "date" },
      createdBy:   { bsonType: "string", maxLength: 128 },
      updatedBy:   { bsonType: "string", maxLength: 128 },
      isDeleted:   { bsonType: "bool" },
      version:     { bsonType: "int", minimum: 1 }                
    }
  } },
  // правило поза $jsonSchema (аналог CHECK): схвалений/відхилений відгук мусить мати запис модерації
  { $or: [ { status: "Pending" }, { moderation: { $exists: true } } ] }
  ] }
});

db.createCollection("reviewReports", {
  validator: { $jsonSchema: {
    bsonType: "object",
    required: ["reviewId", "reporterCustomerId", "reason", "status", "createdAt", "createdBy",
               "updatedAt", "updatedBy", "version"],
    properties: {
      reviewId:           { bsonType: "objectId" },
      reporterCustomerId: { bsonType: "binData" },
      reason:             { enum: ["Spam", "Offensive", "FakeReview", "Other"] },
      comment:            { bsonType: "string", maxLength: 500 },
      status:             { enum: ["Open", "Resolved", "Dismissed"] },
      createdAt:          { bsonType: "date" },
      resolvedAt:         { bsonType: "date" },
      resolvedBy:         { bsonType: "binData" },
      resolution:         { bsonType: "string", maxLength: 500 },
      createdBy:          { bsonType: "string", maxLength: 128 },
      updatedAt:          { bsonType: "date" },
      updatedBy:          { bsonType: "string", maxLength: 128 },
      version:            { bsonType: "int", minimum: 1 }
    }
  } }
});

db.createCollection("productRatingSummaries", {
  validator: { $jsonSchema: {
    bsonType: "object",
    required: ["_id", "count", "sum", "average", "distribution", "version", "updatedAt"],
    properties: {
      _id:     { bsonType: "binData" },                    // = productId
      count:   { bsonType: "int", minimum: 0 },
      sum:     { bsonType: "int", minimum: 0 },
      average: { bsonType: "double", minimum: 0, maximum: 5 },
      distribution: {
        bsonType: "object",
        required: ["1", "2", "3", "4", "5"],
        properties: {
          "1": { bsonType: "int" }, "2": { bsonType: "int" }, "3": { bsonType: "int" },
          "4": { bsonType: "int" }, "5": { bsonType: "int" }
        }
      },
      version:   { bsonType: "long" },                     // монотонно зростає, для порядку подій
      updatedAt: { bsonType: "date" }
    }
  } }
});

db.createCollection("reviewEligibility", {
  validator: { $jsonSchema: {
    bsonType: "object",
    required: ["customerId", "productId", "orderItemId", "orderId", "eligibleSince"],
    properties: {
      customerId:    { bsonType: "binData" },
      productId:     { bsonType: "binData" },
      orderItemId:   { bsonType: "binData" },
      orderId:       { bsonType: "binData" },
      eligibleSince: { bsonType: "date" },
      reviewId:      { bsonType: ["objectId", "null"] }    // null, доки відгук не залишено
    }
  } }
});

db.createCollection("outbox");
db.createCollection("inbox");

/* ---------- Індекси -------------------------------------------------- */

// reviews
db.reviews.createIndex({ orderItemId: 1 }, { unique: true });                    // один відгук на покупку
db.reviews.createIndex({ productId: 1, status: 1, createdAt: -1 },
                       { partialFilterExpression: { isDeleted: false } });   // відгуки товару: рівність, рівність, сортування (ESR); запит містить isDeleted: false
db.reviews.createIndex({ customerId: 1, createdAt: -1 },
                       { partialFilterExpression: { isDeleted: false } });   // відгуки клієнта
db.reviews.createIndex({ createdAt: 1 },
                       { partialFilterExpression: { status: "Pending", isDeleted: false } });  // черга модерації

// reviewReports
db.reviewReports.createIndex({ reviewId: 1, reporterCustomerId: 1 }, { unique: true }); // одна скарга від клієнта; префікс reviewId обслуговує «скарги на відгук»
db.reviewReports.createIndex({ createdAt: 1 }, { partialFilterExpression: { status: "Open" } }); // черга скарг на розгляд

// reviewEligibility
db.reviewEligibility.createIndex({ orderItemId: 1 }, { unique: true });
db.reviewEligibility.createIndex({ customerId: 1, reviewId: 1 });

// outbox / inbox
db.outbox.createIndex({ eventId: 1 }, { unique: true });
db.outbox.createIndex({ createdAt: 1 }, { partialFilterExpression: { processedAt: { $type: "null" } } });
db.inbox.createIndex({ processedAt: 1 }, { expireAfterSeconds: 60 * 60 * 24 * 30 });
