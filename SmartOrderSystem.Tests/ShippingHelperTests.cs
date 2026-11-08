using SmartOrderSystem.Helpers;

namespace SmartOrderSystem.Tests;

public class ShippingHelperTests
{
    [Fact]
    public void CalculateShipping_ReturnsZone1FeeForStoreCity()
    {
        Assert.Equal(80m, ShippingHelper.CalculateShipping(ShippingHelper.StoreCity, false));
    }

    [Fact]
    public void CalculateShipping_ReturnsZone2FeeForNearbyCity()
    {
        Assert.Equal(150m, ShippingHelper.CalculateShipping("REPLACE_ME_1", false));
    }

    [Fact]
    public void CalculateShipping_ReturnsOtherZoneFeeForUnknownCity()
    {
        Assert.Equal(250m, ShippingHelper.CalculateShipping("Somewhere Else", false));
    }

    [Fact]
    public void CalculateShipping_ReturnsOtherZoneFeeForNullCity()
    {
        Assert.Equal(250m, ShippingHelper.CalculateShipping(null, false));
    }

    [Fact]
    public void CalculateShipping_IsFreeForFirstOrderInAnyCity()
    {
        Assert.Equal(0m, ShippingHelper.CalculateShipping("Somewhere Else", true));
    }

    [Fact]
    public void CalculateShipping_ChargesZoneFeeForNotFirstOrder()
    {
        Assert.Equal(150m, ShippingHelper.CalculateShipping("REPLACE_ME_2", false));
    }

    [Fact]
    public void CalculateTotals_DoesNotMakeLargeSubtotalFree()
    {
        var totals = ShippingHelper.CalculateTotals(5000m, "Somewhere Else", false);

        Assert.Equal(5000m, totals.Subtotal);
        Assert.Equal(250m, totals.Shipping);
        Assert.Equal(5250m, totals.Total);
    }

    [Fact]
    public void CalculateShipping_MatchesCityIgnoringCaseAndExtraSpaces()
    {
        Assert.Equal(80m, ShippingHelper.CalculateShipping($"  {ShippingHelper.StoreCity.ToLowerInvariant()}  ", false));
    }
}
