namespace UserManagementApi.Models;

public class IdempotencyRecord
{
    public int Id { get; set; }  // ← EF will pick this up as the PK
    public string Key { get; set; } = default!;   // the Idempotency-Key header
    public string Operation { get; set; } = default!;   // e.g. "CreateUser" or "CreateCar"
    public int ResourceId { get; set; }               // the newly created entity’s Id
    public DateTime CreatedAt { get; set; }               // timestamp for cleanup
}