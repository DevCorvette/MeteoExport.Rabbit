using System.ComponentModel.DataAnnotations.Schema;
using Corvette.MeteoExport.Contracts;
using Corvette.MeteoExport.Core.Models;

namespace Corvette.MeteoExport.Core.Entities;

/// <summary>
/// Задание на выгрузку исторической погоды.
/// </summary>
[Table("export_jobs")]
public class ExportJobEntity
{
    /// <summary>
    /// Идентификатор задания, он же публичный — уходит клиенту в ответе и в ссылках.
    /// </summary>
    /// <remarks>
    /// Guid версии 7: старшие разряды — метка времени, поэтому значения растут и не фрагментируют
    /// индекс, оставаясь при этом неперебираемыми.
    /// </remarks>
    [Column("id")]
    public Guid Id { get; set; }

    [Column("created_at")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Момент, когда воркер взялся за задание, UTC.
    /// </summary>
    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Момент, когда задание пришло к любому из конечных статусов, UTC.
    /// </summary>
    [Column("finished_at")]
    public DateTime? FinishedAt { get; set; }

    /// <summary>
    /// Стадия жизненного цикла задания.
    /// </summary>
    [Column("status")]
    public ExportStatus Status { get; set; }

    /// <summary>
    /// Текст ошибки, с которой задание перешло в <see cref="ExportStatus.Failed"/>.
    /// </summary>
    [Column("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Отпечаток запроса — sha256 его канонического вида.
    /// </summary>
    /// <remarks>
    /// Уникален среди незавершённых заданий: повтор того же запроса, пока предыдущий в работе,
    /// возвращает существующее задание вместо второй такой же выгрузки.
    /// </remarks>
    [Column("request_hash")]
    public string RequestHash { get; set; } = null!;

    /// <summary>
    /// Точки, для которых заказана выгрузка.
    /// </summary>
    /// <remarks>
    /// Колонку jsonb и её имя задаёт ToJson в <see cref="MeteoExportDbContext"/> — атрибут
    /// [Column] на навигации не работает.
    /// </remarks>
    public List<ExportPoint> Points { get; set; } = [];

    /// <summary>
    /// Первые сутки диапазона выгрузки включительно.
    /// </summary>
    [Column("from_date")]
    public DateOnly FromDate { get; set; }

    /// <summary>
    /// Последние сутки диапазона выгрузки включительно.
    /// </summary>
    [Column("to_date")]
    public DateOnly ToDate { get; set; }

    /// <summary>
    /// Заказанные погодные величины — имена параметров Open-Meteo, они же колонки итогового CSV.
    /// </summary>
    [Column("variables")]
    public string[] Variables { get; set; } = [];

    /// <summary>
    /// Сколько кусков работы предстоит — считается один раз при старте.
    /// </summary>
    [Column("chunks_total")]
    public int ChunksTotal { get; set; }

    /// <summary>
    /// Сколько кусков работы уже выполнено.
    /// </summary>
    [Column("chunks_done")]
    public int ChunksDone { get; set; }

    /// <summary>
    /// Адрес для письма о завершении, если задан.
    /// </summary>
    [Column("email")]
    public string? Email { get; set; }

    /// <summary>
    /// Адрес для вебхука о завершении, если задан.
    /// </summary>
    [Column("webhook_url")]
    public string? WebhookUrl { get; set; }

    /// <summary>
    /// Путь к собранному CSV относительно корня хранилища.
    /// </summary>
    [Column("result_file_path")]
    public string? ResultFilePath { get; set; }
}
