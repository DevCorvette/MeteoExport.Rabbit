using Corvette.MeteoExport.Worker.Services;
using Microsoft.Extensions.Hosting;

namespace Corvette.MeteoExport.Worker.HostedServices;

/// <summary>
/// Готовит сервис к работе, пока шина ещё не подключилась.
/// </summary>
public class StartupService : IHostedService
{
    private readonly DraftFiles _drafts;
    private readonly ResultStorage _storage;

    public StartupService(DraftFiles drafts, ResultStorage storage)
    {
        _drafts = drafts ?? throw new ArgumentNullException(nameof(drafts));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _drafts.Prepare();
        await _storage.EnsureBucketAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
