using Microsoft.Extensions.Diagnostics.HealthChecks;
using NotesApp.Api.Data;

namespace NotesApp.Api.Health;

public class DatabaseHealthCheck : IHealthCheck
{
    // O Neon pode estar "dormindo" (scale-to-zero); a primeira conexão acorda o banco e pode demorar.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly AppDbContext _context;

    public DatabaseHealthCheck(AppDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(Timeout);

        try
        {
            return await _context.Database.CanConnectAsync(cts.Token)
                ? HealthCheckResult.Healthy("Banco de dados acessível")
                : HealthCheckResult.Unhealthy("Não foi possível conectar ao banco de dados");
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            return HealthCheckResult.Unhealthy("Tempo esgotado ao conectar ao banco de dados");
        }
    }
}
