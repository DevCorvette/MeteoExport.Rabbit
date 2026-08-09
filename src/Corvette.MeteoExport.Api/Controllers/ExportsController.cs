using System.Net.Mime;
using Corvette.MeteoExport.Api.Models;
using Corvette.MeteoExport.Api.Services;
using Corvette.MeteoExport.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
    private readonly IDbContextFactory<MeteoExportDbContext> _contextFactory;
    private readonly ExportPublisher _publisher;
    private readonly ILogger<CreateExportRequest> _logger;

    public ExportsController(
        IDbContextFactory<MeteoExportDbContext> contextFactory,
        ExportPublisher publisher,
        ILogger<CreateExportRequest> requestLogger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _logger = requestLogger ?? throw new ArgumentNullException(nameof(requestLogger));
    }

    /// <summary>
    /// Заказывает выгрузку. Повтор того же запроса возвращает уже заведённое задание.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<CreateExportResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
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

        var jobId = await request.SaveAsync(_contextFactory, _publisher, _logger, cancellationToken);
        return Accepted($"/exports/{jobId}", new CreateExportResponse { JobId = jobId });
    }

    /// <summary>
    /// Отдаёт состояние задания.
    /// </summary>
    [HttpGet("{jobId:guid}")]
    [ProducesResponseType<ExportStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExportStatusResponse>> GetAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var status = await ExportStatusResponse.LoadAsync(_contextFactory, jobId, cancellationToken);
        if (status == null)
            return NotFound();

        return status;
    }
}
