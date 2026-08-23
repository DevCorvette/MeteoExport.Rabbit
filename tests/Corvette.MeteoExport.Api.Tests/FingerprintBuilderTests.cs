using Corvette.MeteoExport.Api.Services;

namespace Corvette.MeteoExport.Api.Tests;

public class FingerprintBuilderTests
{
    [Fact]
    public void ToHash_SameValues_ReturnsSameHash()
    {
        var first = new FingerprintBuilder();
        first.Append("Москва");
        first.Append(55.75);
        first.Append(new DateOnly(2020, 1, 1));

        var second = new FingerprintBuilder();
        second.Append("Москва");
        second.Append(55.75);
        second.Append(new DateOnly(2020, 1, 1));

        Assert.Equal(first.ToHash(), second.ToHash());
    }

    /// <summary>
    /// Ради этого случая в формат и вписана длина: без неё два соседних поля неотличимы от одного
    /// склеенного, и запрос на две точки совпал бы по отпечатку с запросом на одну.
    /// </summary>
    [Fact]
    public void ToHash_TwoFieldsAgainstOneGlued_ReturnsDifferentHash()
    {
        var separate = new FingerprintBuilder();
        separate.Append("a");
        separate.Append("b");

        var glued = new FingerprintBuilder();
        glued.Append("ab");

        Assert.NotEqual(separate.ToHash(), glued.ToHash());
    }

    [Fact]
    public void ToHash_NullAndEmptyText_ReturnsSameHash()
    {
        var withNull = new FingerprintBuilder();
        withNull.Append((string?)null);

        var withEmpty = new FingerprintBuilder();
        withEmpty.Append(string.Empty);

        Assert.Equal(withNull.ToHash(), withEmpty.ToHash());
    }

    /// <summary>
    /// Соседние представимые числа должны расходиться: координаты пишутся форматом R как раз затем,
    /// чтобы не потерять младшие разряды.
    /// </summary>
    [Fact]
    public void ToHash_NeighbouringDoubles_ReturnsDifferentHash()
    {
        var value = new FingerprintBuilder();
        value.Append(55.75);

        var neighbour = new FingerprintBuilder();
        neighbour.Append(Math.BitIncrement(55.75));

        Assert.NotEqual(value.ToHash(), neighbour.ToHash());
    }

    [Fact]
    public void ToHash_TwoNumbersAgainstOneGlued_ReturnsDifferentHash()
    {
        var separate = new FingerprintBuilder();
        separate.Append(12);
        separate.Append(3);

        var glued = new FingerprintBuilder();
        glued.Append(123);

        Assert.NotEqual(separate.ToHash(), glued.ToHash());
    }
}
