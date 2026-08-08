using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Corvette.MeteoExport.Core.Design;

/// <summary>
/// Фабрика контекста для инструментов EF.
/// </summary>
/// <remarks>
/// Нужна, чтобы dotnet ef migrations add умел создать контекст без запуска приложения
/// </remarks>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MeteoExportDbContext>
{
    private const string ConnectionStringVariable = "METEOEXPORT_CONNECTION_STRING";

    private const string DefaultConnectionString = "Host=localhost;Port=5434;Database=meteoexport;Username=meteoexport;Password=meteoexport";

    public MeteoExportDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = DefaultConnectionString;

        var builder = new DbContextOptionsBuilder<MeteoExportDbContext>();
        MeteoExportDbContextFactory.Configure(builder, connectionString, loggerFactory: null);
        return new MeteoExportDbContext(builder.Options);
    }
}
