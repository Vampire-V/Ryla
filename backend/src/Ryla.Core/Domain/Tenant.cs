namespace Ryla.Core.Domain;

/// <summary>
/// Tenant = หนึ่งธุรกิจ SME ไทย
/// สร้างอัตโนมัติเมื่อ user สมัครใหม่ผ่าน Supabase Auth trigger
/// </summary>
public sealed record Tenant(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt);
