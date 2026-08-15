using System.Globalization;
using System.Text;
using System.Text.Json;
using Corvette.MeteoExport.Core.Entities;
using Corvette.MeteoExport.Core.Models;
using Corvette.MeteoExport.Worker.OpenMeteo;
using Microsoft.Extensions.Logging;

namespace Corvette.MeteoExport.Worker.Services;

/// <summary>
/// Собирает файл выгрузки по заданию: ходит в Open-Meteo кусками и пишет CSV.
/// </summary>
public class ExportBuilder
{
    /// <summary>
    /// Сколько точек уходит одним запросом.
    /// </summary>
    private const int PointsPerRequest = 25;

    /// <summary>
    /// За сколько суток забираются данные одним запросом.
    /// </summary>
    private const int DaysPerRequest = 365;

    /// <summary>
    /// Колонки, которые идут в CSV перед заказанными величинами.
    /// </summary>
    private static readonly string[] HeadColumns = ["latitude", "longitude", "name", "time"];

    /// <summary>
    /// Не чаще этого прогресс уходит в базу.
    /// </summary>
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Символы, из-за которых подпись точки приходится брать в кавычки.
    /// </summary>
    private static readonly char[] QuotedChars = [',', '"', '\r', '\n'];

    private readonly OpenMeteoClient _client;
    private readonly ExportJobRepository _repository;
    private readonly ILogger<ExportBuilder> _logger;

    public ExportBuilder(OpenMeteoClient client, ExportJobRepository repository, ILogger<ExportBuilder> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Пишет черновик выгрузки в указанный файл.
    /// </summary>
    public async Task BuildAsync(ExportJobEntity job, string draftPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (string.IsNullOrWhiteSpace(draftPath)) throw new ArgumentException("Не задан путь к черновику.", nameof(draftPath));

        // Кусок работы — пачка точек за отрезок дат, то есть ровно один запрос в Open-Meteo.
        var batches = job.Points.Chunk(PointsPerRequest).ToList();

        // режем диапазон на отрезки
        var ranges = new List<(DateOnly From, DateOnly To)>();
        var start = job.FromDate;
        while (start <= job.ToDate)
        {
            var end = DateOnly.FromDayNumber(Math.Min(start.DayNumber + DaysPerRequest - 1, job.ToDate.DayNumber));
            ranges.Add((start, end));
            start = end.AddDays(1);
        }

        var chunksTotal = batches.Count * ranges.Count;

        await _repository.SaveProgressAsync(job.Id, chunksDone: 0, chunksTotal, cancellationToken);
        var progressSavedAt = DateTime.UtcNow;

        _logger.LogInformation($"Выгрузка начата (JobId=\"{job.Id}\", ChunksTotal={chunksTotal}, Batches={batches.Count}, Ranges={ranges.Count})");

        await using (var writer = new StreamWriter(draftPath, append: false))
        {
            // заголовок
            await writer.WriteLineAsync(string.Join(',', HeadColumns.Concat(job.Variables)));

            var chunksDone = 0;

            // Даты снаружи, точки внутри: в файле сначала лежит весь первый отрезок дат.
            foreach (var range in ranges)
            {
                foreach (var batch in batches)
                {
                    var responses = await _client.LoadAsync(batch, range.From, range.To, job.Variables, cancellationToken);
                    await WriteChunkAsync(writer, batch, responses, job.Variables, cancellationToken);

                    chunksDone++;

                    // Прогресс нужен, чтобы клиент видел движение, а не для точности.
                    // Последний кусок пишем всегда: на нём счётчик сходится с итогом.
                    if (DateTime.UtcNow - progressSavedAt >= ProgressInterval || chunksDone == chunksTotal)
                    {
                        await _repository.SaveProgressAsync(job.Id, chunksDone, chunksTotal, cancellationToken);
                        progressSavedAt = DateTime.UtcNow;
                    }

                    _logger.LogDebug($"Кусок готов (JobId=\"{job.Id}\", ChunksDone={chunksDone}, ChunksTotal={chunksTotal}, From=\"{range.From:O}\", To=\"{range.To:O}\", Points={batch.Length})");
                }
            }
        }
    }

    /// <summary>
    /// Дописывает в файл строки одного куска — по строке на точку и час.
    /// </summary>
    private static async Task WriteChunkAsync(
        StreamWriter writer,
        IReadOnlyList<ExportPoint> points,
        IReadOnlyList<LocationResponse> responses,
        string[] variables,
        CancellationToken cancellationToken)
    {
        var row = new StringBuilder();

        for (var pointIndex = 0; pointIndex < points.Count; pointIndex++)
        {
            var point = points[pointIndex];
            var latitude = point.Latitude.ToString(CultureInfo.InvariantCulture);
            var longitude = point.Longitude.ToString(CultureInfo.InvariantCulture);

            var hourly = responses[pointIndex].Hourly ?? throw new InvalidOperationException($"В ответе Open-Meteo нет почасовых рядов (Latitude={latitude}, Longitude={longitude}).");

            // ряды заказанных величин
            var columns = new List<JsonElement[]>(variables.Length);
            foreach (var variable in variables)
            {
                if (!hourly.Values.TryGetValue(variable, out var column) || column.ValueKind != JsonValueKind.Array)
                    throw new InvalidOperationException($"В ответе Open-Meteo нет ряда значений (Variable=\"{variable}\").");

                var values = column.EnumerateArray().ToArray();
                if (values.Length != hourly.Time.Count)
                    throw new InvalidOperationException($"Ряд значений не сходится с рядом времени (Variable=\"{variable}\", Values={values.Length}, Times={hourly.Time.Count}).");

                columns.Add(values);
            }

            // подпись в кавычках, если внутри разделитель
            var name = point.Name ?? string.Empty;
            if (name.IndexOfAny(QuotedChars) >= 0)
            {
                name = $"\"{name.Replace("\"", "\"\"")}\"";
            }

            // Координаты пишем заказанные: в ответе они подтянуты к узлу сетки и у соседних точек совпадут.
            var head = $"{latitude},{longitude},{name},";

            for (var hour = 0; hour < hourly.Time.Count; hour++)
            {
                row.Clear();
                row.Append(head).Append(hourly.Time[hour]);

                foreach (var column in columns)
                {
                    var value = column[hour];
                    row.Append(',').Append(value.ValueKind == JsonValueKind.Null ? string.Empty : value.GetRawText());
                }

                await writer.WriteLineAsync(row, cancellationToken);
            }
        }
    }
}
