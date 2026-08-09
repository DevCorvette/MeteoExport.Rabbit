using Corvette.MeteoExport.Core;
using Corvette.MeteoExport.Core.Entities;
using Corvette.MeteoExport.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Corvette.MeteoExport.Api.Models;

/// <summary>
/// Состояние задания на выгрузку.
/// </summary>
public class ExportStatusResponse
{
    /// <summary>
    /// Идентификатор задания.
    /// </summary>
    public Guid JobId { get; }

    /// <summary>
    /// Стадия жизненного цикла задания.
    /// </summary>
    public ExportStatus Status { get; }

    /// <summary>
    /// Когда задание принято, UTC.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Когда за задание взялись, UTC.
    /// </summary>
    public DateTime? StartedAt { get; }

    /// <summary>
    /// Когда задание пришло к своему исходу, UTC.
    /// </summary>
    public DateTime? FinishedAt { get; }

    /// <summary>
    /// Сколько работы сделано.
    /// </summary>
    public ExportProgress Progress { get; }

    /// <summary>
    /// Текст ошибки, если задание не удалось.
    /// </summary>
    public string? Error { get; }

    public ExportStatusResponse(ExportJobEntity job)
    {
        ArgumentNullException.ThrowIfNull(job);

        JobId = job.Id;
        Status = job.Status;
        CreatedAt = job.CreatedAt;
        StartedAt = job.StartedAt;
        FinishedAt = job.FinishedAt;
        Progress = new ExportProgress(job.ChunksDone, job.ChunksTotal);
        Error = job.Error;
    }

    /// <summary>
    /// Читает состояние задания из базы; null — задания с таким идентификатором нет.
    /// </summary>
    public static async Task<ExportStatusResponse?> LoadAsync(
        IDbContextFactory<MeteoExportDbContext> contextFactory,
        Guid jobId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);

        await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            var job = await context.ExportJobs
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == jobId, cancellationToken);

            if (job == null)
                return null;

            return new ExportStatusResponse(job);
        }
    }
}

/// <summary>
/// Сколько кусков работы сделано из скольких.
/// </summary>
public record ExportProgress(int Done, int Total);
