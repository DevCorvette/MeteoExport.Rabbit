using System.Net.Mime;
using Corvette.MeteoExport.Api.Models;
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
    private readonly ILogger<ExportsController> _logger;

    public ExportsController(
        MeteoExportDbContext context,
        ISendEndpointProvider sendEndpointProvider,
        ILogger<ExportsController> requestLogger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _sendEndpointProvider = sendEndpointProvider ?? throw new ArgumentNullException(nameof(sendEndpointProvider));
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
}
