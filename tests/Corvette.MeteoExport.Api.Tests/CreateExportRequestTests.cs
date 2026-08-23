using Corvette.MeteoExport.Api.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Corvette.MeteoExport.Api.Tests;

public class CreateExportRequestTests
{
    /// <summary>
    /// На сколько суток архив отстаёт от сегодняшнего дня — столько же, сколько в самом запросе.
    /// </summary>
    private const int ArchiveLagDays = 5;

    private static readonly ILogger Logger = NullLogger.Instance;

    private static readonly ExportPointModel Moscow = new() { Latitude = 55.75, Longitude = 37.62, Name = "Москва" };
    private static readonly ExportPointModel Piter = new() { Latitude = 59.94, Longitude = 30.31, Name = "Питер" };

    /// <summary>
    /// Последние сутки, которые архив уже отдаёт: даты в тестах считаем от них, иначе набор констант
    /// протух бы через неделю.
    /// </summary>
    private static DateOnly LastAvailable => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-ArchiveLagDays);

    [Fact]
    public void Validate_GoodRequest_ReturnsNoErrors()
    {
        var errors = CreateRequest().Validate(Logger);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_NoPoints_ReturnsError()
    {
        var errors = CreateRequest(points: []).Validate(Logger);

        Assert.Contains(errors, x => x.Contains("Не задана ни одна точка"));
    }

    [Fact]
    public void Validate_TooManyPoints_ReturnsError()
    {
        var points = CreatePoints(101);

        var errors = CreateRequest(points).Validate(Logger);

        Assert.Contains(errors, x => x.Contains("Точек больше допустимого"));
    }

    [Theory]
    [InlineData(91, 37.62)]
    [InlineData(-91, 37.62)]
    [InlineData(55.75, 181)]
    [InlineData(55.75, -181)]
    public void Validate_CoordinateOutOfRange_ReturnsError(double latitude, double longitude)
    {
        var point = new ExportPointModel { Latitude = latitude, Longitude = longitude };

        var errors = CreateRequest([point]).Validate(Logger);

        Assert.Contains(errors, x => x.Contains("вне допустимых значений"));
    }

    [Fact]
    public void Validate_FromAfterTo_ReturnsError()
    {
        var errors = CreateRequest(from: LastAvailable, to: LastAvailable.AddDays(-2)).Validate(Logger);

        Assert.Contains(errors, x => x.Contains("Начало диапазона позже конца"));
    }

    [Fact]
    public void Validate_FromBeforeArchiveStart_ReturnsError()
    {
        var errors = CreateRequest(from: new DateOnly(1939, 12, 31)).Validate(Logger);

        Assert.Contains(errors, x => x.Contains("Архив начинается позже"));
    }

    /// <summary>
    /// Свежие сутки архив ещё не досчитал, заказывать их нельзя, хотя дата уже наступила.
    /// </summary>
    [Fact]
    public void Validate_ToNewerThanArchive_ReturnsError()
    {
        var errors = CreateRequest(to: LastAvailable.AddDays(1)).Validate(Logger);

        Assert.Contains(errors, x => x.Contains("Данных за этот срок в архиве ещё нет"));
    }

    [Fact]
    public void Validate_UnknownVariable_ReturnsErrorWithAllowedList()
    {
        var errors = CreateRequest(variables: ["temperature_2m", "teperature_2m"]).Validate(Logger);

        Assert.Contains(errors, x => x.Contains("Неизвестная величина"));
        Assert.Contains(errors, x => x.Contains("Допустимые величины"));
    }

    /// <summary>
    /// Повторённая опечатка — одна ошибка, а не две.
    /// </summary>
    [Fact]
    public void Validate_SameUnknownVariableTwice_ReturnsSingleError()
    {
        var errors = CreateRequest(variables: ["teperature_2m", "teperature_2m"]).Validate(Logger);

        Assert.Single(errors, x => x.Contains("Неизвестная величина"));
    }

    [Fact]
    public void Validate_TooLargeRequest_ReturnsError()
    {
        var to = LastAvailable;
        var from = to.AddDays(-3649);

        var errors = CreateRequest(CreatePoints(100), from, to, ["temperature_2m", "precipitation", "rain"]).Validate(Logger);

        Assert.Contains(errors, x => x.Contains("Запрос слишком объёмный"));
    }

    [Fact]
    public void Validate_MalformedEmail_ReturnsError()
    {
        var errors = CreateRequest(email: "клиент собака example.com").Validate(Logger);

        Assert.Contains(errors, x => x.Contains("Адрес почты не разобран"));
    }

    [Fact]
    public void Validate_WebhookWithoutScheme_ReturnsError()
    {
        var errors = CreateRequest(webhookUrl: "example.com/hook").Validate(Logger);

        Assert.Contains(errors, x => x.Contains("Адрес вебхука"));
    }

    [Fact]
    public void ComputeHash_PointsInOtherOrder_ReturnsSameHash()
    {
        var straight = CreateRequest([Moscow, Piter]).ComputeHash();
        var reversed = CreateRequest([Piter, Moscow]).ComputeHash();

        Assert.Equal(straight, reversed);
    }

    /// <summary>
    /// Точка, названная дважды, выгружается один раз — значит и запрос это тот же самый.
    /// </summary>
    [Fact]
    public void ComputeHash_DuplicatePoint_ReturnsSameHashAsSingle()
    {
        var single = CreateRequest([Moscow]).ComputeHash();
        var duplicated = CreateRequest([Moscow, Moscow]).ComputeHash();

        Assert.Equal(single, duplicated);
    }

    /// <summary>
    /// Умолчания подставляются до отпечатка: запрос без величин и запрос с теми же величинами
    /// явно означают одно и то же.
    /// </summary>
    [Fact]
    public void ComputeHash_DefaultVariablesWrittenExplicitly_ReturnsSameHash()
    {
        var byDefault = CreateRequest(variables: null).ComputeHash();
        var written = CreateRequest(variables: ["temperature_2m", "precipitation"]).ComputeHash();

        Assert.Equal(byDefault, written);
    }

    [Fact]
    public void ComputeHash_VariablesInOtherOrder_ReturnsSameHash()
    {
        var straight = CreateRequest(variables: ["temperature_2m", "precipitation"]).ComputeHash();
        var reversed = CreateRequest(variables: ["precipitation", "temperature_2m"]).ComputeHash();

        Assert.Equal(straight, reversed);
    }

    [Fact]
    public void ComputeHash_EmailInOtherCase_ReturnsSameHash()
    {
        var lower = CreateRequest(email: "client@example.com").ComputeHash();
        var upper = CreateRequest(email: " Client@Example.COM ").ComputeHash();

        Assert.Equal(lower, upper);
    }

    /// <summary>
    /// На сам файл адреса не влияют, а на то, кому он достанется, — влияют, поэтому одним запросом
    /// такие два считать нельзя.
    /// </summary>
    [Fact]
    public void ComputeHash_OtherEmail_ReturnsDifferentHash()
    {
        var first = CreateRequest(email: "first@example.com").ComputeHash();
        var second = CreateRequest(email: "second@example.com").ComputeHash();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ComputeHash_OtherDates_ReturnsDifferentHash()
    {
        var shorter = CreateRequest(from: LastAvailable.AddDays(-2)).ComputeHash();
        var longer = CreateRequest(from: LastAvailable.AddDays(-3)).ComputeHash();

        Assert.NotEqual(shorter, longer);
    }

    private static CreateExportRequest CreateRequest(
        ExportPointModel[]? points = null,
        DateOnly? from = null,
        DateOnly? to = null,
        string[]? variables = null,
        string? email = null,
        string? webhookUrl = null) =>
        new()
        {
            Points = points ?? [Moscow],
            From = from ?? LastAvailable.AddDays(-2),
            To = to ?? LastAvailable,
            Variables = variables,
            Email = email,
            WebhookUrl = webhookUrl,
        };

    /// <summary>
    /// Точки с неповторяющимися координатами: одинаковые схлопнулись бы при подсчёте объёма.
    /// </summary>
    private static ExportPointModel[] CreatePoints(int count) =>
        [.. Enumerable.Range(0, count).Select(x => new ExportPointModel { Latitude = 50 + x * 0.01, Longitude = 37 })];
}
