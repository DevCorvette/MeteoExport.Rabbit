using Corvette.MeteoExport.Core.Entities;
using Corvette.MeteoExport.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Corvette.MeteoExport.Core;

/// <summary>
/// Контекст базы данных MeteoExport.
/// </summary>
/// <remarks>
/// Свойства сущностей настраиваются атрибутами, а всё, что относится к схеме БД —
/// индексы, связи, дефолты и типы, специфичные для PostgreSQL — настраивается здесь.
/// </remarks>
public class MeteoExportDbContext : DbContext
{
    public MeteoExportDbContext(DbContextOptions<MeteoExportDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Задания на выгрузку исторической погоды.
    /// </summary>
    public DbSet<ExportJobEntity> ExportJobs => Set<ExportJobEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ExportJobEntity>(entity =>
        {
            // Имена ключей и индексов задаём явно: дефолтные (PK_export_jobs) выбиваются из snake_case.
            entity.HasKey(x => x.Id)
                .HasName("pk_export_jobs");

            // Ид генерирует приложение
            entity.Property(x => x.Id)
                .ValueGeneratedNever();

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("now()");

            // Имя колонки для jsonb
            entity.OwnsMany(x => x.Points, points =>
            {
                points.ToJson("points");

                // Ключи внутри документа задаём явно, по умолчанию берутся имена свойств C#.
                points.Property(x => x.Latitude).HasJsonPropertyName("latitude");
                points.Property(x => x.Longitude).HasJsonPropertyName("longitude");
                points.Property(x => x.Name).HasJsonPropertyName("name");
            });

            // Дедупликация повторно отправленного запроса. Индекс частичный
            entity.HasIndex(x => x.RequestHash)
                .IsUnique()
                .HasFilter($"status in ({(int)ExportStatus.Queued}, {(int)ExportStatus.Running})")
                .HasDatabaseName("uix_export_jobs_request_hash_active");

            // Под подметающий проход по заданиям, чья команда не опубликовалась.
            entity.HasIndex(x => new { x.Status, x.CreatedAt })
                .HasDatabaseName("ix_export_jobs_status_created_at");
        });
    }
}
