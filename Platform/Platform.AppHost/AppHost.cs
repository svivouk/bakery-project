var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sql-password", secret: true);
var sql = builder.AddSqlServer("sql", sqlPassword).WithDataVolume();
var ordersDb = sql.AddDatabase("OrdersDb");
var catalogDb = sql.AddDatabase("CatalogDb");
var redis = builder.AddRedis("redis").WithDataVolume().WithRedisInsight();
var mongo = builder.AddMongoDB("mongo").WithDataVolume();
var reviewsDb = mongo.AddDatabase("ReviewsDb");
var rabbit = builder.AddRabbitMQ("messaging")
    .WithDataVolume()
    .WithManagementPlugin();

var orders = builder.AddProject<Projects.Orders_Api>("orders-api")
    .WithReference(ordersDb).WaitFor(ordersDb)
    .WithReference(redis).WaitFor(redis)
    .WithHttpHealthCheck("/health/ready");

var catalog = builder.AddProject<Projects.Catalog_API>("catalog-api")
    .WithReference(catalogDb).WaitFor(catalogDb)
    .WithReference(redis).WaitFor(redis)
    .WithHttpHealthCheck("/health/ready");
var reviews = builder.AddProject<Projects.Reviews_WebApi>("reviews-api")
    .WithReference(reviewsDb).WaitFor(reviewsDb)
    .WithReference(redis).WaitFor(redis)
    .WithHttpHealthCheck("/health/ready");

var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(orders).WaitFor(orders)
    .WithReference(catalog).WaitFor(catalog)
    .WithReference(reviews)
    .WithReference(redis).WaitFor(redis)
    .WithHttpHealthCheck("/health/ready");

builder.AddProject<Projects.Platform_ApiGateway>("api-gateway")
    .WithReference(orders)
    .WithReference(catalog)
    .WithReference(reviews)
    .WithReference(aggregator).WaitFor(aggregator)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health/ready");

catalog.WithReference(rabbit).WaitFor(rabbit);     
orders.WithReference(rabbit).WaitFor(rabbit);      
reviews.WithReference(rabbit).WaitFor(rabbit);

builder.Build().Run();
