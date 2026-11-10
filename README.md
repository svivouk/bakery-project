# Інтернет-пекарня/кондитерська: проєктування персистентності

Навчальний проєкт: три незалежні контексти, кожен зі своєю БД і своєю технологією доступу до даних.

## Структура репозиторію

```text
├── README.md    опис домену, запуск, розгортання БД, таблиця дублювання
├── docs/        ERD-діаграми, опис контекстів, приклади документів
│   ├── erd.md                 ERD трьох контекстів (Mermaid) і карта контекстів
│   ├── contexts.md            опис контекстів, агрегати, життєвий цикл, взаємодія
│   └── mongo-documents.md     приклади документів MongoDB, індекси, типові операції
├── Platform/    рішення .NET: Aspire AppHost, Gateway, Aggregator і сервіси (розділи 10–11)
└── db/          SQL- і MongoDB-скрипти
    ├── p1/      Проєкт №1 (Каталог): schema.sql · procedures.sql · seed.sql
    ├── p2/      Проєкт №2 (Замовлення): schema.sql · seed.sql
    └── p3/      Проєкт №3 (Відгуки, MongoDB): collections.js · seed.js
```


## 1. Опис домену

Інтернет-пекарня/кондитерська — це вебсистема, якою користуються **покупці** (замовляють випічку, торти та десерти з доставкою або самовивозом), **персонал** пекарні (керує асортиментом, цінами та виконанням замовлень) і **адміністратори**. Система показує актуальний каталог товарів, приймає й обробляє замовлення та збирає відгуки покупців, щоб бізнес міг продавати без ручної обробки заявок і покращувати якість продукції.

## 2. Контексти та технології

| Контекст | Відповідальність | БД | Технологія | Скрипти |
|---|---|---|---|---|
| **Каталог** (Проєкт №1) | Що пекарня продає: товари, варіанти, ціни, алергени, залишки | `catalog_db` (SQL Server) | ADO.NET + Dapper, збережувані процедури | `db/p1/` |
| **Замовлення** (Проєкт №2) | Покупка: клієнти, кошик, замовлення, оплата, доставка, промокоди | `orders_db` (SQL Server) | EF Core, Code First, Fluent API, міграції | `db/p2/` (еталонна схема) |
| **Відгуки** (Проєкт №3) | Оцінки, коментарі, модерація, рейтинг | `reviews_db` (MongoDB) | MongoDB, документи-агрегати | `db/p3/` |

Міжконтекстних зовнішніх ключів немає. Контексти посилаються один на одного лише ідентифікаторами та знімками полів, а синхронізуються подіями.

```mermaid
flowchart LR
    C["Каталог<br/>catalog_db"]
    O["Замовлення<br/>orders_db"]
    R["Відгуки<br/>reviews_db"]
    C -- "ProductRenamed, PriceChanged,<br/>ProductDeactivated" --> O
    C -- "ProductRenamed" --> R
    O -- "OrderCompleted, CustomerDeleted" --> R
    R -- "RatingSummaryChanged" --> C
    O -. "синхронно: резерв залишку" .-> C
```

## 3. Сутності та агрегати

Усього 20 сутностей, кожна належить рівно одному контексту.

| Контекст | Агрегат (корінь + склад) | Примітка |
|---|---|---|
| Каталог | **Product** = Product + ProductVariant (+ зв'язки з Ingredient, Allergen) | Варіанти не існують без товару |
| Каталог | **Category**, **Ingredient**, **Allergen** | Довідники |
| Каталог | **StockItem** (+ технічна таблиця резервів) | Окремий агрегат: змінюється постійно |
| Замовлення | **Customer** = Customer + Address | |
| Замовлення | **Cart** = Cart + CartItem | Тимчасовий |
| Замовлення | **Order** = Order + OrderItem + Delivery | Після оформлення позиції не змінюються |
| Замовлення | **Payment**, **PromoCode** | Власний життєвий цикл |
| Відгуки | **Review** = Review + ReviewPhoto + ReviewReply (вбудовані) | Один документ |
| Відгуки | **ReviewReport**, **ProductRatingSummary** | Окремі колекції |

Службові структури (не входять у 20 сутностей): `OutboxMessages`, `InboxMessages`, `StockReservations`, `ProductRefs`/`VariantRefs`, `ProductRatings`, `PromoCodeUsages`, колекція `reviewEligibility`.

## 4. Запуск і розгортання БД

### Вимоги

- SQL Server 2019+ (скрипти на T-SQL; потрібні `STRING_AGG`, `CREATE OR ALTER`, `THROW`)
- MongoDB 6+ та `mongosh`
- `sqlcmd` (або будь-який клієнт SQL Server)

### Розгортання (з кореня репозиторію)

```bash
SQL="sqlcmd -S localhost,1433 -U sa -C -f 65001 -b"

# Проєкт №1 — Каталог: схема → процедури → демо-дані
$SQL -i db/p1/schema.sql
$SQL -i db/p1/procedures.sql
$SQL -i db/p1/seed.sql

# Проєкт №2 — Замовлення: схема → демо-дані
$SQL -i db/p2/schema.sql
$SQL -i db/p2/seed.sql

# Проєкт №3 — Відгуки (MongoDB): колекції та індекси → демо-дані
mongosh "mongodb://localhost:27017" db/p3/collections.js
mongosh "mongodb://localhost:27017" db/p3/seed.js
```

Порядок важливий: у кожному проєкті спочатку схема, потім решта. Між проєктами залежностей під час розгортання немає: БД незалежні.

### Примітки

- Прапорець `-f 65001` потрібен, бо скрипти містять кирилицю в UTF-8; `-b` зупиняє виконання на першій помилці.
- `schema.sql` (обидва SQL-проєкти) і `db/p3/collections.js` призначені для **порожніх** БД. Файли `seed.sql` і `db/p3/seed.js` можна запускати повторно: вони спершу очищають дані у своїх таблицях/колекціях (структура, валідатори й індекси лишаються). Не запускайте їх на БД із реальними даними. Для перезапуску видаліть БД (`DROP DATABASE catalog_db;`, `DROP DATABASE orders_db;`, а в `reviews_db` виконайте `db.dropDatabase()`).
- **Демонстраційні дані узгоджені між БД.** Усі три `seed`-файли використовують спільні ідентифікатори: ті самі товари й варіанти в Каталозі та в `ProductRefs` / `VariantRefs` Замовлень, ті самі клієнти й позиції замовлень у Замовленнях і Відгуках. Рейтинг «Медовика» у Відгуках збігається з копією `ProductRatings` у Каталозі.
- У Проєкті №2 схема в реальному коді створюється **міграціями EF Core**:
  ```bash
  dotnet ef migrations add Initial
  dotnet ef database update
  ```
  Fluent API має відтворювати `db/p2/schema.sql` (кластеризований індекс `OrderItems`, фільтровані індекси, `CHECK`-обмеження, `OnDelete`, `IsRowVersion`). Сам скрипт потрібен для рев'ю та порівняння.
- Перевірка після розгортання: `SELECT * FROM dbo.vw_ProductListing;` у `catalog_db`, `SELECT * FROM dbo.vw_OrderSummary;` в `orders_db`, `db.reviews.find()` у `reviews_db`.

## 5. Таблиця політики дублювання даних

Власник даних один. Інші контексти тримають ідентифікатор і, за потреби, знімок або копію. Оновлення йдуть **подіями через брокер** з Outbox-патерном у власника. Споживачі ідемпотентні (Inbox) і відкидають застарілі події за версією.

**Знімок** фіксує факт на момент події й ніколи не змінюється. **Копія** відображає поточний стан власника й оновлюється подіями.

| Поле | Де зберігається | Джерело (власник) | Політика узгодженості |
|---|---|---|---|
| `OrderItems.ProductName`, `VariantName`, `UnitPrice` | БД Замовлень | Product / ProductVariant (Каталог) | **Знімок** на момент оформлення, не оновлюється |
| `ProductRefs`: `Name`, `IsActive`; `VariantRefs`: `Name`, `Price`, `IsActive` (локальна копія довідника) | БД Замовлень | Product / ProductVariant (Каталог) | **Оновлюється подіями** `ProductRenamed`, `PriceChanged`, `ProductDeactivated`. Eventual consistency; застарілі події відкидаються за `SourceVersion` |
| `CartItems.ProductId`, `VariantId` | БД Замовлень | Product (Каталог) | Лише **ідентифікатор**. Якщо товар вимкнено, позиція кошика стає «недоступна» |
| `StockItems.QuantityAvailable` | **Не дублюється** | StockItem (Каталог) | Строга узгодженість: синхронний виклик резервування під час оформлення; не вдалося — замовлення не створюється |
| `Review.customerId` | БД Відгуків | Customer (Замовлення) | Лише **ідентифікатор** |
| `Review.customerDisplayName` | БД Відгуків | Customer (Замовлення) | **Знімок** на момент відгуку. Виняток: подія `CustomerDeleted` запускає анонімізацію |
| `reviewEligibility`: `customerId`, `productId`, `orderItemId` | БД Відгуків | Order / OrderItem (Замовлення) | **Оновлюється подією** `OrderCompleted`. Відгук можливий лише за наявності запису; один відгук на `orderItemId` |
| `Review.productId` | БД Відгуків | Product (Каталог) | Лише **ідентифікатор** |
| `Review.productName` | БД Відгуків | Product (Каталог) | **Оновлюється подією** `ProductRenamed` |
| `ProductRatings`: `AvgRating`, `ReviewCount` (копія `ProductRatingSummary`) | БД Каталогу | ProductRatingSummary (Відгуки) | **Оновлюється подією** `RatingSummaryChanged`. Eventual consistency; застарілі події відкидаються за `RatingVersion` |

Відгуки не потребують нічого з Замовлень понад наведене; Каталогу не потрібні дані із Замовлень.

## 6. Ключі, індекси, обмеження

- **Первинні ключі:** GUID для основних сутностей (id потрібен до `INSERT`, потрапляє в події й інші контексти). `BIGINT IDENTITY` лише для `OutboxMessages`. `OrderNumber` (BIGINT із `SEQUENCE`) окремо для людей. Репліки (`ProductRefs`, `VariantRefs`) беруть id власника.
- **Каскади:** `CASCADE` лише для залежних рядків без власної історії (`OrderItems`, `Deliveries`, `CartItems` тощо). Клієнта з історією не видаляють, а анонімізують; `Payments` не зникають разом із замовленням; варіант товару не видаляють фізично.
- **Індекси** прив'язані до запитів (покриваючі та фільтровані): `Status = 1` для списків товарів, `IsActive = 1` для цін варіантів, `Status = 0` для прострочених резервів. Індекси без запиту не створюються.
- **Обмеження:** `CHECK` на суми, статуси, формат телефону й email, узгодженість `Payments` (статус, дата оплати, сума повернення), інваріанти видалення.

Деталі та обґрунтування: коментарі в шапці `db/p1/schema.sql`, `db/p2/schema.sql`, `db/p3/collections.js`.

## 7. Аудит, м'яке видалення, конкурентність

| Механізм | Що зроблено |
|---|---|
| Аудит | `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` на кореневих сутностях. `*By` має формат `staff:<guid>`, `customer:<guid>` або `system:<сервіс>`. Заповнює застосунок, а не тригери |
| М'яке видалення | `IsDeleted` для Category, Product, ProductVariant, Ingredient, PromoCode, Review. Для Customer це похідне від `AnonymizedAt`. Замовлення та платежі не видаляються. Унікальність `Name` / `Slug` / `Sku` / `Code` забезпечують фільтровані індекси `WHERE IsDeleted = 0` |
| Оптимістична конкурентність (SQL) | `ROWVERSION` на рядках, які редагують люди: Product, ProductVariant, Customer, Cart, Order, Payment, PromoCode. Конфлікт повертається як HTTP 409 |
| Оптимістична конкурентність (Mongo) | Поле `version` у `reviews` і `reviewReports`: `updateOne({_id, version: N}, {..., $inc: {version: 1}})` |
| Без токена | `StockItems` і лічильник промокоду змінюються з кожним замовленням, тому використовують атомарний `UPDATE` з умовою. Колонки, які пише система (`ProductRatings`, `PromoCodeUsages`), винесено в окремі таблиці, щоб не викликати хибних конфліктів |

## 8. Події

| Джерело | Події |
|---|---|
| Каталог | `ProductCreated`, `ProductRenamed`, `PriceChanged`, `ProductDeactivated`, `StockChanged` |
| Замовлення | `OrderPlaced`, `OrderCancelled`, `OrderCompleted`, `CustomerDeleted` |
| Відгуки | `RatingSummaryChanged` |

Версія події для SQL-джерел: `CONVERT(BIGINT, RowVersion)` рядка-джерела.

## 9. Статус

- Проєкт №1 (Каталог) реалізований у `Platform/`: Web API, BLL, DAL (ADO.NET + Dapper), Aspire AppHost. Опис і запуск: розділ 10. Повна платформа (Gateway, Aggregator, Orders, Reviews, Redis): розділ 11.
- Проєкти №2 (EF Core) і №3 (MongoDB) у цьому репозиторії поки що лише описані схемами, демо-даними й правилами узгодженості.
- SQL-скрипти Каталогу виконуються автоматично при старті AppHost, тож перший запуск через Aspire — це і є їхня перша перевірка на живому SQL Server. Скрипти Замовлень і MongoDB на живих серверах ще не запускалися. Перед використанням перевірте плани виконання ключових запитів (`SET STATISTICS IO` або Query Store).
- Відомий розбіг у демо-даних: `db/p2/seed.sql` (`ProductRefs`) використовує ідентифікатори товарів `b0000000-…`, а `db/p1/seed.sql` і `db/p3/seed.js` — `a0000000-…`. Для узгодженості між контекстами p2 треба привести до `a0000000-…`.

## 10. Застосунок «Каталог» (Проєкт №1): архітектура, запуск, запити

### Схема рішення

```mermaid
flowchart LR
    AH["Platform<br/>(Aspire AppHost)"] -- "запускає" --> API
    AH -- "контейнер + скрипти db/p1" --> SQL[("SQL Server<br/>catalog_db")]
    API["Catalog.API<br/>контролери, middleware, воркер"] --> BLL["Catalog.BLL<br/>сервіси, DTO, AutoMapper"]
    BLL --> DAL["Catalog.DAL<br/>Unit of Work, ADO.NET + Dapper"]
    DAL --> DOM["Catalog.Domain<br/>моделі, винятки"]
    DAL --> SQL
```

| Проєкт (папка) | Роль |
|---|---|
| `Platform/Platform.AppHost` | Aspire AppHost: піднімає SQL Server, Redis і MongoDB, запускає сервіси, Gateway та Aggregator і передає їм рядки підключення |
| `Platform/Services/Catalog/Catlog.API` | Тонкі контролери, `ExceptionHandlingMiddleware` (ProblemDetails), воркер очищення прострочених резервацій, Swagger |
| `Platform/Services/Catalog/Catlog.BLL` | Сервіси, DTO з DataAnnotations, профіль AutoMapper (`CatalogProfile`), доменні перевірки (`Check`) |
| `Platform/Services/Catalog/Catlog.DAL` | `IUnitOfWork` (одне з'єднання й транзакція), репозиторії: ADO.NET (`CategoryRepository`) і Dapper (`BaseDapperRepository<T>`) |
| `Platform/Services/Catalog/Catalog.Domain` | Моделі, enum-и, доменні винятки |

### Що потрібно для запуску

- .NET SDK 10;
- Docker Desktop (або сумісний контейнерний рантайм): Aspire запускає SQL Server в контейнері;
- `sqlcmd`, SQL Server і ручне розгортання БД з розділу 4 для цього шляху **не потрібні**.

### Запуск через Aspire

```bash
dotnet run --project Platform/Platform.AppHost
# або з каталогу Platform/Platform.AppHost:  aspire run
```

1. У консолі з'явиться посилання на Aspire Dashboard. Відкрийте його.
2. Дочекайтеся зеленого статусу ресурсів `sql`, `CatalogDb` і `catalog-api`. Перший старт довший: завантажується образ SQL Server і виконуються скрипти `schema.sql` → `procedures.sql` → `seed.sql`.
3. У Dashboard натисніть на endpoint `catalog-api` і додайте `/swagger` (Swagger UI працює в середовищі Development).

Порт API у Aspire динамічний: беріть його з Dashboard. Рядок підключення API отримує через `WithReference` (змінна `ConnectionStrings__CatalogDb`), вона перекриває значення з `appsettings.json`.

Контейнер SQL Server створюється заново при кожному запуску, тому дані повертаються до демо-стану. Щоб зберігати їх між запусками, додайте `.WithDataVolume()` разом із фіксованим паролем (`builder.AddParameter("sql-password", secret: true)` → `AddSqlServer("sql", password)`): з тимчасово згенерованим паролем том буде недоступний на наступному старті.

**Запуск API без Aspire.** Розгорніть БД за розділом 4, вкажіть свій рядок підключення в `Platform/Catlog.API/appsettings.json` (`ConnectionStrings:CatalogDb`) і виконайте `dotnet run --project Platform/Services/Catalog/Catlog.API` (за замовчуванням `http://localhost:5065`).

### Приклади запитів

Нижче `API` — адреса API (з Dashboard або `http://localhost:5065`). Ідентифікатори взято з `db/p1/seed.sql`. Те саме можна виконати в Swagger UI; для Postman імпортуйте `{API}/swagger/v1/swagger.json`.

```bash
API=http://localhost:5065

# Категорії
curl "$API/api/categories"
curl -X POST "$API/api/categories" -H "Content-Type: application/json" \
  -d '{"name":"Печиво","slug":"pechyvo","description":"Домашнє печиво","sortOrder":5}'   # 201 + Location

# Вітрина: фільтри, сортування (Newest | PriceAsc | PriceDesc | Rating), пагінація
curl "$API/api/products?categorySlug=torty&inStockOnly=true&sort=PriceAsc&page=1&pageSize=10"
curl "$API/api/products/top-rated?count=5"
curl "$API/api/products/by-slug/medovyk"

# Адмін-частина: товар з варіантами та rowVersion
curl "$API/api/products/a0000000-0000-4000-8000-000000000001"
curl -X POST "$API/api/products" -H "Content-Type: application/json" -d '{
  "categoryId": "c0000000-0000-4000-8000-000000000001",
  "name": "Червоний оксамит", "slug": "chervonyi-oksamyt",
  "description": "Бісквітний торт із крем-чізом", "basePrice": 700,
  "weightGrams": 1200, "shelfLifeHours": 72,
  "ingredientIds": ["a0000000-0000-4000-8000-000000000001"],
  "variants": [{"name": "1,2 кг", "sku": "RV-1200", "price": 700, "initialQuantity": 5, "dailyLimit": 8}]
}'
# Оновлення (PUT /api/products/{id}) вимагає "rowVersion" з останнього GET. Застарілий токен -> 409.

# Довідники
curl "$API/api/ingredients"
curl "$API/api/allergens"

# Залишки та резервування
curl "$API/api/stock/d0000000-0000-4000-8000-000000000001"
curl -X PUT "$API/api/stock/d0000000-0000-4000-8000-000000000001" -H "Content-Type: application/json" \
  -d '{"quantityAvailable":10,"dailyLimit":10}'
curl -X POST "$API/api/stock/reservations" -H "Content-Type: application/json" -d '{
  "orderId": "11111111-1111-4111-8111-111111111111", "ttlMinutes": 15,
  "lines": [{"variantId": "d0000000-0000-4000-8000-000000000001", "quantity": 2}]
}'                                                                                   # все або нічого; 409, якщо не вистачає
curl -X POST   "$API/api/stock/reservations/11111111-1111-4111-8111-111111111111/commit"   # 204
curl -X DELETE "$API/api/stock/reservations/11111111-1111-4111-8111-111111111111"          # звільнити

# Обслуговування (зазвичай викликає планувальник)
curl -X POST "$API/api/stock/maintenance/release-expired"
curl -X POST "$API/api/stock/maintenance/reset-daily"

curl -X POST "$API/api/events/rating-summary-changed" -H "Content-Type: application/json" \
  -d '{"eventId":"22222222-2222-4222-8222-222222222222","productId":"a0000000-0000-4000-8000-000000000001","avgRating":4.5,"reviewCount":13,"version":13}'
```

### Формат помилок

Усі помилки повертаються як `application/problem+json`:

| Статус | Коли |
|---|---|
| 400 | порушено DataAnnotations на DTO (`errors` по полях) або доменна `ValidationException` з BLL |
| 404 | `NotFoundException`: категорію, товар, варіант чи резервацію не знайдено |
| 409 | `ConflictException` (дублікат slug / SKU), `ConcurrencyException` (застарілий `rowVersion`), `InsufficientStockException` |
| 500 | неочікувана помилка (деталі лише в лозі) |

Приклад: `POST /api/categories` з `{"name":"","slug":"Bad Slug"}` повертає 400 з повідомленнями для полів `Name` і `Slug`.

## 11. Мікросервісна платформа: запуск і запити через Gateway

Розділ 10 описує один сервіс «Каталог». Тут описано, як запустити всю платформу й користуватися нею через єдину точку входу.

### Склад платформи

```mermaid
flowchart LR
    Client["Клієнт<br/>(curl, браузер)"] --> GW["Gateway (YARP)<br/>api-gateway"]
    GW --> CAT["catalog-api"]
    GW --> ORD["orders-api"]
    GW --> REV["reviews-api"]
    GW --> AGG["aggregator"]
    AGG -- "gRPC" --> CAT
    AGG -- "HTTP" --> ORD
    AGG -- "HTTP" --> REV
    CAT --> SQLC[("SQL Server<br/>CatalogDb")]
    ORD --> SQLO[("SQL Server<br/>OrdersDb")]
    REV --> MONGO[("MongoDB<br/>ReviewsDb")]
    CAT & AGG --> REDIS[("Redis")]
```

| Ресурс | Проєкт | Роль | Протокол |
|---|---|---|---|
| `api-gateway` | `Platform/Platform.ApiGateway` | Єдина точка входу, маршрутизація YARP, CorrelationId, логування | HTTP/HTTPS |
| `catalog-api` | `Platform/Services/Catalog/Catlog.API` | Каталог, залишки; REST + gRPC (`catalog.v1.CatalogGrpc`) | HTTPS (потрібен для gRPC) |
| `orders-api` | `Platform/Services/Orders/Orders.Api` | Клієнти, кошик, замовлення, платежі, промокоди | HTTP/HTTPS |
| `reviews-api` | `Platform/Services/Reviews/Reviews.WebApi` | Відгуки, рейтинг (MongoDB) | HTTP/HTTPS |
| `aggregator` | `Platform/Platform.Aggregator` | Складає повне замовлення з трьох сервісів; Каталог читає через gRPC | HTTP/HTTPS |
| `redis` | контейнер | Розподілений кеш Каталогу й Aggregator | TCP |
| `sql`, `mongo` | контейнери | Бази даних (`CatalogDb`, `OrdersDb`, `ReviewsDb`) | TCP |

gRPC-контракти: `Platform/Services/Catalog/Catlog.API/Protos/catalog.proto`. Він використовується як сервером (Catalog), так і клієнтом (Aggregator). gRPC доступний лише всередині платформи, зовнішні клієнти працюють через HTTP.

### Вимоги

- .NET SDK 10 і Docker Desktop (SQL Server, MongoDB і Redis піднімаються в контейнерах);
- довірений dev-сертифікат, інакше gRPC-виклики Aggregator → Catalog завершаться помилкою SSL: `dotnet dev-certs https --trust` (один раз);
- ручне розгортання БД не потрібне: Каталог створює схему зі скриптів при старті, Замовлення застосовують міграції EF Core, Відгуки заповнюють MongoDB сідером (лише в Development).

### Запуск

```bash
dotnet run --project Platform/Platform.AppHost
# або з каталогу Platform/Platform.AppHost:  aspire run
```

1. У консолі з'явиться посилання на Aspire Dashboard, відкрийте його.
2. Дочекайтеся стану `Running` для всіх ресурсів: `sql`, `mongo`, `redis`, `catalog-api`, `orders-api`, `reviews-api`, `aggregator`, `api-gateway`. Перший старт довший: завантажуються образи й виконується ініціалізація баз.
3. Адреси сервісів беріть із колонки **Endpoints** у Dashboard. Порти проєктів задані в їхніх `Properties/launchSettings.json`, у типовому запуску це:

| Ресурс | HTTP | HTTPS |
|---|---|---|
| `api-gateway` | `http://localhost:5000` | `https://localhost:7275` |
| `aggregator` | `http://localhost:5225` | `https://localhost:7247` |
| `catalog-api` | `http://localhost:5065` | `https://localhost:7185` |
| `orders-api` | `http://localhost:5188` | `https://localhost:7179` |
| `reviews-api` | `http://localhost:5012` | `https://localhost:7276` |

Якщо порти у вас інші, користуйтеся значеннями з Dashboard. У прикладах нижче `GW=http://localhost:5000`.

> **Hot Reload і Aspire.** Після змін у коді конструкторів, DI або `.proto` повністю зупиніть AppHost і запустіть знову. Hot Reload тут призводить до `HotReloadException`.
>
> **HTTP і HTTPS.** Не надсилайте `http://` на HTTPS-порт (і навпаки): `curl` відповість `Empty reply from server`. Для HTTPS із dev-сертифікатом додайте `-k`.
>
> **PowerShell.** Використовуйте `curl.exe` (а не `curl`, який є псевдонімом `Invoke-WebRequest`); продовження рядка: зворотний апостроф `` ` ``.

### Gateway: маршрути

Усі зовнішні запити йдуть через Gateway. Маршрутизацію описує секція `ReverseProxy` у `Platform/Platform.ApiGateway/appsettings.json` (маршрути `Routes`, кластери `Clusters`, адреси сервісів за іменами ресурсів AppHost). Gateway додає `X-Correlation-Id` (якщо клієнт його не надіслав) і повертає його у відповіді.

| Префікс шляху | Куди направляється | Приклад |
|---|---|---|
| `/api/products`, `/api/categories`, `/api/ingredients`, `/api/allergens`, `/api/stock`, `/api/events` | `catalog-api` | `GET /api/products/by-slug/medovyk` |
| `/api/orders` | `orders-api` | `GET /api/orders/{id}` |
| `/api/reviews` | `reviews-api` | `GET /api/reviews?productId={id}` |
| `/api/aggregator` | `aggregator` | `GET /api/aggregator/orders/{id}/full` |

Шлях, для якого немає маршруту, повертає `404`, а в логах Gateway лишається маршрут `none`. Якщо префікс у вашому `appsettings.json` відрізняється від таблиці, орієнтуйтеся на `Routes`.

### Приклади запитів через Gateway

```bash
GW=http://localhost:5000
```

**Каталог** (демо-ідентифікатори з `db/p1/seed.sql`):

```bash
curl "$GW/api/categories"
curl "$GW/api/products?categorySlug=torty&inStockOnly=true&sort=PriceAsc&page=1&pageSize=10"
curl "$GW/api/products/top-rated?count=5"
curl "$GW/api/products/by-slug/medovyk"
curl "$GW/api/stock/d0000000-0000-4000-8000-000000000001"
```

**Замовлення.** У БД Замовлень демо-даних про самі замовлення немає, є клієнт `c1000000-0000-4000-8000-000000000001` і його кошик. Оформіть замовлення в Swagger Orders (`https://localhost:7179/swagger`, метод `POST /api/orders`, `customerId` = клієнт із сідера) і скопіюйте `id` із відповіді:

```bash
ORDER_ID=<id із відповіді POST /api/orders>
curl "$GW/api/orders/$ORDER_ID"
```

**Відгуки** (`productId` = «Медовик»):

```bash
curl "$GW/api/reviews?productId=a0000000-0000-4000-8000-000000000001&pageSize=10"
```

**Aggregator: повне замовлення** (замовлення + товари з Каталогу + відгуки на ці товари):

```bash
curl -i "$GW/api/aggregator/orders/$ORDER_ID/full"
```

Відповідь `200`:

```json
{
  "order":    { "id": "…", "orderNumber": 1001, "status": "…", "totalAmount": 1300.00, "items": [ … ] },
  "products": [ { "productId": "a0000000-…", "name": "Медовик", "slug": "medovyk", "basePrice": 650.00 } ],
  "reviews":  [ { "id": "…", "productId": "a0000000-…", "rating": 5, "authorName": "…" } ],
  "warnings": []
}
```

| Статус | Коли |
|---|---|
| `200` | Замовлення знайдено. Якщо `catalog-api` або `reviews-api` недоступний, відповідь **часткова**: відповідні масиви порожні, а в `warnings` є повідомлення на кшталт `"catalog-api is unavailable"` |
| `404` | Замовлення не знайдено |
| `503` | недоступний `orders-api` (без нього відповідь неможлива) |

Aggregator викликає Каталог через gRPC і виконує запити до Каталогу та Відгуків паралельно. Успішна (повна, без `warnings`) відповідь кешується в Redis на 30 с під ключем `aggregator:order:{id}:full`, тож повторний запит у цьому вікні не звертається до сервісів. Часткові відповіді не кешуються. Перевірка часткової відповіді: зупиніть `catalog-api` у Dashboard, зачекайте 30 с і повторіть запит.

Aggregator доступний і напряму (мінуючи Gateway), наприклад для налагодження: `curl "http://localhost:5225/api/aggregator/orders/$ORDER_ID/full"`.

### Swagger і службові адреси

Swagger UI є в середовищі Development: `catalog-api`, `orders-api` та `reviews-api` публікують його за `/swagger` на власному порту (див. таблицю портів). Стан сервісів: `/health` та `/alive`; стан кешу Каталогу: `https://localhost:7185/health/cache` (hit ratio, лічильники по рівнях L1/Redis, окремо для gRPC).

### Кеш Каталогу

Каталог кешує лише читання вітрини (картку товару за slug, лістинги, топ, категорії, довідники) у два рівні: пам'ять процесу (L1) і Redis (L2). Запис через REST (`POST/PUT/DELETE`) синхронно інвалідує зв'язані ключі. Після `PUT /api/products/{id}` наступний `GET /api/products/by-slug/{slug}` повертає оновлені дані. Зупинка Redis не зупиняє сервіс: запити йдуть у БД, а в логах з'являються попередження. Список товарів і топ скидаються разом через ключ-версію `catalog:lists:version`.

### Телеметрія в Aspire Dashboard

- **Traces:** наскрізний ланцюжок Gateway → Aggregator → Catalog (gRPC-spans клієнта й сервера в одному trace) → SQL. На серверних spans є події `cache.hit`/`cache.miss` та атрибути `correlation.id`, `cache.result`.
- **Structured logs:** кожен запит має `CorrelationId` (його можна передати у заголовку `X-Correlation-Id`); для gRPC-викликів Каталогу є рядки `gRPC {Method} from {Peer}: {Status} in {Elapsed} ms`, для кешу `Cache hit/miss/set/invalidated`.
- **Metrics:** meter `Catalog.Cache`: `cache.hits`, `cache.misses` (атрибут `cache.level` = `memory` або `redis`), `cache.invalidations`, `cache.evictions`, `cache.operation.duration`, `cache.memory.entries`.