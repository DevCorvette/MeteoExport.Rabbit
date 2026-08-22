using System.Net.Mime;
using Amazon.S3;
using Corvette.MeteoExport.Api.Models;
using Corvette.MeteoExport.Api.Settings;
using Corvette.MeteoExport.Core;
using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace Corvette.MeteoExport.Api.Controllers;

/// <summary>
/// Приём запросов на выгрузку и состояние заданий.
/// </summary>
[ApiController]
[Route("exports")]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
public class ExportsController : ControllerBase
{
    private readonly MeteoExportDbContext _context;
    private readonly ISendEndpointProvider _sendEndpointProvider;
    private readonly AmazonS3Client _storage;
    private readonly StorageSettings _storageSettings;
    private readonly ILogger<ExportsController> _logger;

    public ExportsController(
        MeteoExportDbContext context,
        ISendEndpointProvider sendEndpointProvider,
        AmazonS3Client storage,
        StorageSettings storageSettings,
        ILogger<ExportsController> requestLogger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _sendEndpointProvider = sendEndpointProvider ?? throw new ArgumentNullException(nameof(sendEndpointProvider));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _storageSettings = storageSettings ?? throw new ArgumentNullException(nameof(storageSettings));
        _logger = requestLogger ?? throw new ArgumentNullException(nameof(requestLogger));
    }

    /// <summary>
    /// Заказывает выгрузку. Повтор того же запроса возвращает уже заведённое задание.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<CreateExportResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CreateExportResponse>> CreateAsync(
        CreateExportRequest request,
        CancellationToken cancellationToken)
    {
        var errors = request.Validate(_logger);
        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return ValidationProblem(ModelState);
        }

        var jobId = await request.SaveAsync(_context, _sendEndpointProvider, _logger, cancellationToken);
        return Accepted($"/exports/{jobId}", new CreateExportResponse { JobId = jobId });
    }

    /// <summary>
    /// Отдаёт состояние задания.
    /// </summary>
    [HttpGet("{jobId:guid}")]
    [ProducesResponseType<ExportStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ExportStatusResponse>> GetAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var status = await ExportStatusResponse.LoadAsync(_context, jobId, cancellationToken);
        if (status == null)
            return NotFound();

        return status;
    }

    /// <summary>
    /// Отдаёт собранный файл выгрузки.
    /// </summary>
    [HttpGet("{jobId:guid}/file")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK, MediaTypeNames.Text.Csv)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetFileAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var file = new ExportFile { JobId = jobId };
        var state = await file.InitAsync(_context, _storage, _storageSettings, _logger, cancellationToken);

        return state switch
        {
            ExportFileState.Ready    => File(file.Content!, MediaTypeNames.Text.Csv, file.FileName),
            ExportFileState.NotReady => Problem($"Файла у задания нет (Status=\"{file.JobStatus}\").", statusCode: StatusCodes.Status409Conflict),
            ExportFileState.NotFound => NotFound(),
            _                        => throw new ArgumentOutOfRangeException(nameof(state), state, "Неизвестное состояние файла задания."),
        };
    }
}
