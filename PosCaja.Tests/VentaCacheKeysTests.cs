using System.Globalization;
using PosCaja.Infrastructure.Redis;

namespace PosCaja.Tests;

public sealed class VentaCacheKeysTests
{
    private const string Prefix = "Api.Redis:Development:pos_caja:venta:v1:";
    private static readonly Guid VentaId = Guid.Parse("abcdef01-2345-6789-abcd-ef0123456789");

    [Fact]
    public void VentaKeyContainsPrefixNormalizedGuidAndPos()
    {
        Assert.Equal(
            Prefix + "item:abcdef0123456789abcdef0123456789:12",
            VentaCacheKeys.ForVenta(Prefix, VentaId, 12));
    }

    [Fact]
    public void EquivalentGuidRepresentationsProduceSameKey()
    {
        var equivalentId = Guid.Parse("ABCDEF0123456789ABCDEF0123456789");

        Assert.Equal(
            VentaCacheKeys.ForVenta(Prefix, VentaId, 12),
            VentaCacheKeys.ForVenta(Prefix, equivalentId, 12));
    }

    [Fact]
    public void DifferentPosValuesDoNotShareVentaKey()
    {
        Assert.NotEqual(
            VentaCacheKeys.ForVenta(Prefix, VentaId, 1),
            VentaCacheKeys.ForVenta(Prefix, VentaId, 2));
    }

    [Fact]
    public void DifferentVentaIdsDoNotShareKey()
    {
        Assert.NotEqual(
            VentaCacheKeys.ForVenta(Prefix, VentaId, 1),
            VentaCacheKeys.ForVenta(Prefix, Guid.Empty, 1));
    }

    [Fact]
    public void PageKeyContainsBothPaginationParameters()
    {
        Assert.Equal(Prefix + "page:1:size:50", VentaCacheKeys.ForPage(Prefix, 1, 50));
    }

    [Fact]
    public void RepeatedPageQueryProducesSameKey()
    {
        var first = VentaCacheKeys.ForPage(Prefix, 1, 50);

        Assert.Equal(first, VentaCacheKeys.ForPage(Prefix, 1, 50));
    }

    [Theory]
    [InlineData(2, 50)]
    [InlineData(1, 10)]
    [InlineData(2, 10)]
    public void DifferentPaginationParametersDoNotShareKey(int page, int size)
    {
        Assert.NotEqual(
            VentaCacheKeys.ForPage(Prefix, 1, 50),
            VentaCacheKeys.ForPage(Prefix, page, size));
    }

    [Fact]
    public void PrefixSeparatesEnvironmentsForBothQueryTypes()
    {
        const string production = "Api.Redis:Production:pos_caja:venta:v1:";

        Assert.NotEqual(
            VentaCacheKeys.ForVenta(Prefix, VentaId, 1),
            VentaCacheKeys.ForVenta(production, VentaId, 1));
        Assert.NotEqual(
            VentaCacheKeys.ForPage(Prefix, 1, 50),
            VentaCacheKeys.ForPage(production, 1, 50));
    }

    [Fact]
    public void IndividualAndPageQueriesHaveDistinctNamespaces()
    {
        var itemKey = VentaCacheKeys.ForVenta(Prefix, VentaId, 1);
        var pageKey = VentaCacheKeys.ForPage(Prefix, 1, 50);

        Assert.StartsWith(Prefix + "item:", itemKey);
        Assert.StartsWith(Prefix + "page:", pageKey);
        Assert.NotEqual(itemKey, pageKey);
    }

    [Fact]
    public void NumericFormattingDoesNotDependOnCurrentCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var customCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        customCulture.NumberFormat.NegativeSign = "NEG";
        try
        {
            CultureInfo.CurrentCulture = customCulture;

            // Valores negativos únicamente para detectar una conversión dependiente
            // de cultura. La validación de paginación pertenece a VentaService.
            Assert.Equal(
                Prefix + "item:abcdef0123456789abcdef0123456789:-12",
                VentaCacheKeys.ForVenta(Prefix, VentaId, -12));
            Assert.Equal(
                Prefix + "page:-1:size:-50",
                VentaCacheKeys.ForPage(Prefix, -1, -50));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
