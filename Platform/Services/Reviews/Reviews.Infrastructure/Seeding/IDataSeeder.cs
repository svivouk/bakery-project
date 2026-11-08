namespace Reviews.Infrastructure.Seeding;

/// <summary>Початкові (демонстраційні) дані. Реалізація має бути ідемпотентною: повторний запуск нічого не дублює.</summary>
public interface IDataSeeder
{
    Task SeedAsync(CancellationToken ct = default);
}
