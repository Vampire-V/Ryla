using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ryla.Infrastructure.Adapters.Database;

/// <summary>
/// Startup DB probe — fail-fast ถ้า PostgreSQL ไม่พร้อม
/// รัน SELECT 1 ก่อน Kestrel accept connections
///
/// Rationale: app นี้เป็น webhook service ที่ต้องการ DB เพื่อ function
/// ถ้า DB ไม่พร้อม → start ต่อไม่มีประโยชน์ + ซ่อนปัญหา (webhook 200
/// ทั้งที่ LINE push ล้มเหลว) → fail-fast ชัดเจนกว่า
///
/// Local dev: developer เห็น error ทันทีแทนที่จะเจอผ่าน webhook hours later
/// Production: cloud host (Azure Functions/K8s) restart with backoff
/// จนกว่า DB จะกลับมา — ถูกต้องสำหรับ stateful service
/// </summary>
internal sealed class DbStartupProbe : IHostedService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<DbStartupProbe> _logger;
    private readonly TimeSpan _timeout;

    public DbStartupProbe(
        IDbConnectionFactory connectionFactory,
        ILogger<DbStartupProbe> logger,
        IConfiguration configuration)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;

        // อ่าน env var แบบ AOT-safe (ไม่ใช้ ConfigurationBinder.GetValue<T> — IL2026)
        var raw = configuration["STARTUP_DB_PROBE_TIMEOUT_SECONDS"];
        var timeoutSeconds = int.TryParse(raw, out var parsed) ? parsed : 5;
        _timeout = TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeout);

        try
        {
            await using var connection = await _connectionFactory.CreateAsync(cts.Token);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT 1";
            var result = await cmd.ExecuteScalarAsync(cts.Token);

            _logger.LogInformation(
                "Database startup probe OK (SELECT 1 returned {Result}, host={Host})",
                result, connection.Host);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Database startup probe FAILED. " +
                "Fix: 'make dev-start' (local dev) or check ConnectionStrings:Supabase (production). " +
                "App will exit to prevent silent webhook failures");

            throw new InvalidOperationException(
                "Database unreachable at startup. See previous log entry for remediation.",
                ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
