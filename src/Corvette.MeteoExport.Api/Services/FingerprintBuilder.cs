using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Corvette.MeteoExport.Api.Services;

/// <summary>
/// Собирает канонический вид значения и сворачивает его в отпечаток.
/// </summary>
public class FingerprintBuilder
{
    private readonly StringBuilder _builder = new();

    /// <summary>
    /// Дописывает произвольный текст; null и пустая строка неразличимы.
    /// </summary>
    /// <remarks>
    /// Впереди пишется длина: она отличает значение с разделителем внутри от пары соседних полей.
    /// </remarks>
    public void Append(string? value)
    {
        var text = value ?? string.Empty;
        _builder.Append(text.Length).Append(':').Append(text).Append('|');
    }

    /// <summary>
    /// Дописывает число в виде, из которого оно читается обратно без потерь.
    /// </summary>
    public void Append(double value)
    {
        _builder.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append('|');
    }

    /// <summary>
    /// Дописывает целое.
    /// </summary>
    public void Append(int value)
    {
        _builder.Append(value).Append('|');
    }

    /// <summary>
    /// Дописывает дату.
    /// </summary>
    public void Append(DateOnly value)
    {
        _builder.Append(value.ToString("O", CultureInfo.InvariantCulture)).Append('|');
    }

    /// <summary>
    /// Сворачивает накопленное в sha256 и отдаёт шестнадцатеричной строкой.
    /// </summary>
    public string ToHash()
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(_builder.ToString()));

        return Convert.ToHexStringLower(hash);
    }
}
