namespace Ryla.Core.Domain;

/// <summary>
/// Profile เชื่อม auth.users.id กับ tenant
/// full_name อาจเป็น null ถ้า user ไม่ได้ให้ข้อมูลตอนสมัคร
/// </summary>
public sealed record UserProfile(
    Guid Id,
    Guid TenantId,
    string? FullName,
    DateTimeOffset CreatedAt);
