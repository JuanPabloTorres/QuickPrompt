namespace QuickPrompt.Domain.Entities;

/// <summary>
/// Base entity for domain models.
/// Pure POCO without any external dependencies (no UI, no ORM attributes).
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsActive { get; set; } = true;
    
    public DateTime? DeletedAt { get; set; }

    protected BaseEntity()
    {
    }

    protected BaseEntity(Guid id)
    {
        Id = id;
    }
}
