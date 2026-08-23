using Corvette.MeteoExport.Api.Models;
using Corvette.MeteoExport.Contracts;
using Corvette.MeteoExport.Core.Entities;

namespace Corvette.MeteoExport.Api.Tests;

public class ExportStatusResponseTests
{
    [Fact]
    public void Constructor_CompletedJob_FillsFileUrl()
    {
        var job = CreateJob(ExportStatus.Completed);

        var response = new ExportStatusResponse(job);

        Assert.Equal($"/exports/{job.Id}/file", response.FileUrl);
    }

    /// <summary>
    /// Ссылка появляется вместе с файлом: у задания в работе и у неудавшегося её нет.
    /// </summary>
    [Theory]
    [InlineData(ExportStatus.Queued)]
    [InlineData(ExportStatus.Running)]
    [InlineData(ExportStatus.Failed)]
    public void Constructor_JobWithoutFile_LeavesFileUrlEmpty(ExportStatus status)
    {
        var job = CreateJob(status);

        var response = new ExportStatusResponse(job);

        Assert.Null(response.FileUrl);
    }

    [Fact]
    public void Constructor_Job_CopiesProgress()
    {
        var job = CreateJob(ExportStatus.Running);
        job.ChunksDone = 3;
        job.ChunksTotal = 7;

        var response = new ExportStatusResponse(job);

        Assert.Equal(3, response.Progress.Done);
        Assert.Equal(7, response.Progress.Total);
    }

    private static ExportJobEntity CreateJob(ExportStatus status) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Status = status,
            CreatedAt = DateTime.UtcNow,
            RequestHash = "hash",
            FromDate = new DateOnly(2020, 1, 1),
            ToDate = new DateOnly(2020, 1, 3),
            Variables = ["temperature_2m"],
            ResultFilePath = status == ExportStatus.Completed ? "exports/file.csv" : null,
        };
}
