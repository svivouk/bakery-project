var builder = DistributedApplication.CreateBuilder(args);
var scriptsDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "db", "p1"));
var creationScript = string.Join(
    Environment.NewLine + "GO" + Environment.NewLine,
    new[] { "schema.sql", "procedures.sql", "seed.sql" }
        .Select(file => File.ReadAllText(Path.Combine(scriptsDir, file))));

var sql = builder.AddSqlServer("sql");
var catalogDb = sql.AddDatabase("CatalogDb", "catalog_db")
                   .WithCreationScript(creationScript);
builder.AddProject<Projects.Catalog_API>("catalog-api")
       .WithReference(catalogDb)
       .WaitFor(catalogDb);

builder.AddProject<Projects.Orders_Api>("orders-api");

builder.Build().Run();
