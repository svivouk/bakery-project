namespace Reviews.Infrastructure.Seeding;

/// <summary>Фіксовані ідентифікатори сусідніх контекстів (Каталог, Замовлення, Працівники). Усі коректні UUID, а кожна покупка
/// унікальна: на orderItemId діє унікальний індекс.</summary>
internal static class SeedData
{
    public static readonly Guid MedovykProductId = Guid.Parse("a0000000-0000-4000-8000-000000000001");   // Медовик
    public static readonly Guid CroissantProductId = Guid.Parse("a0000000-0000-4000-8000-000000000003"); // Круасан класичний

    public static readonly Guid Customer1 = Guid.Parse("c1000000-0000-4000-8000-000000000001");   // Олена Коваленко
    public static readonly Guid Customer2 = Guid.Parse("c1000000-0000-4000-8000-000000000002");   // Андрій Мельник
    public static readonly Guid Moderator = Guid.Parse("d0000000-0000-4000-8000-000000000001");   // працівник пекарні

    public static readonly Guid Order1 = Guid.Parse("e0000000-0000-4000-8000-000000000001");
    public static readonly Guid Order2 = Guid.Parse("e0000000-0000-4000-8000-000000000002");

    public static readonly Guid OrderItemMedovyk1 = Guid.Parse("f0000000-0000-4000-8000-000000000001");   // замовлення №1, Медовик
    public static readonly Guid OrderItemMedovyk2 = Guid.Parse("f0000000-0000-4000-8000-000000000002");   // замовлення №2, Медовик
    public static readonly Guid OrderItemCroissant = Guid.Parse("f0000000-0000-4000-8000-000000000003");  // замовлення №1, Круасани

    public static readonly Guid[] ReviewedOrderItems = [OrderItemMedovyk1, OrderItemMedovyk2];
}
