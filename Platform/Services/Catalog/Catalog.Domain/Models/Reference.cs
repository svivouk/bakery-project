namespace Catalog.Domain.Models;

public class Ingredient : AuditableEntity
{
    public Guid IngredientId { get; set; }
    public string Name { get; set; } = "";
    public bool IsDeleted { get; set; }
}

public class Allergen : AuditableEntity
{
    public Guid AllergenId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}
