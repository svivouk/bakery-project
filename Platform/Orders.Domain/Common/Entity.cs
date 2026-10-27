namespace Orders.Domain;
public abstract class Entity
{
    public Guid Id { get; protected set; }
}
public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTime UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = string.Empty;
}
