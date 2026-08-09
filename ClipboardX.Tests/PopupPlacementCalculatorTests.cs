using System.Drawing;
using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class PopupPlacementCalculatorTests
{
    [Theory]
    [InlineData(400, 200, 300, 1.5, 2.0, 600, 400)]
    [InlineData(400, 0, 300, 1.25, 1.5, 500, 450)]
    public void ToPhysicalSize_UsesActualHeightOrFallsBackToMaxHeight(
        double width,
        double actualHeight,
        double maxHeight,
        double scaleX,
        double scaleY,
        int expectedWidth,
        int expectedHeight)
    {
        var size = PopupPlacementCalculator.ToPhysicalSize(
            width,
            actualHeight,
            maxHeight,
            scaleX,
            scaleY);

        Assert.Equal(new PopupPlacementCalculator.PhysicalSize(expectedWidth, expectedHeight), size);
    }

    [Fact]
    public void PlaceFixedAtTopLeft_AppliesMargin()
    {
        var position = PopupPlacementCalculator.PlaceFixedAtTopLeft(
            new Rectangle(0, 0, 1920, 1080),
            new PopupPlacementCalculator.PhysicalSize(500, 400));

        Assert.Equal(new PopupPlacementCalculator.Position(16, 16), position);
    }

    [Fact]
    public void PlaceFixedAtTopLeft_SupportsNegativeMonitorCoordinates()
    {
        var position = PopupPlacementCalculator.PlaceFixedAtTopLeft(
            new Rectangle(-1920, 0, 1920, 1080),
            new PopupPlacementCalculator.PhysicalSize(500, 400));

        Assert.Equal(new PopupPlacementCalculator.Position(-1904, 16), position);
    }

    [Fact]
    public void PlaceFixedAtTopLeft_ClampsOversizedPopup()
    {
        var position = PopupPlacementCalculator.PlaceFixedAtTopLeft(
            new Rectangle(0, 0, 300, 200),
            new PopupPlacementCalculator.PhysicalSize(400, 300));

        Assert.Equal(new PopupPlacementCalculator.Position(0, 0), position);
    }

    [Fact]
    public void PlaceNearAnchor_KeepsPositionWhenPopupFits()
    {
        var position = PopupPlacementCalculator.PlaceNearAnchor(
            new Rectangle(0, 0, 1920, 1080),
            new PopupPlacementCalculator.PhysicalSize(500, 400),
            100,
            200);

        Assert.Equal(new PopupPlacementCalculator.Position(100, 200), position);
    }

    [Fact]
    public void PlaceNearAnchor_ClampsRightOverflow()
    {
        var position = PopupPlacementCalculator.PlaceNearAnchor(
            new Rectangle(0, 0, 1920, 1080),
            new PopupPlacementCalculator.PhysicalSize(500, 400),
            1800,
            200);

        Assert.Equal(new PopupPlacementCalculator.Position(1420, 200), position);
    }

    [Fact]
    public void PlaceNearAnchor_FlipsAboveBottomOverflow()
    {
        var position = PopupPlacementCalculator.PlaceNearAnchor(
            new Rectangle(0, 0, 1920, 1080),
            new PopupPlacementCalculator.PhysicalSize(500, 300),
            100,
            1000);

        Assert.Equal(new PopupPlacementCalculator.Position(100, 668), position);
    }

    [Fact]
    public void PlaceNearAnchor_ClampsFlippedPopupToWorkAreaTop()
    {
        var position = PopupPlacementCalculator.PlaceNearAnchor(
            new Rectangle(0, 0, 1920, 200),
            new PopupPlacementCalculator.PhysicalSize(500, 300),
            100,
            100);

        Assert.Equal(new PopupPlacementCalculator.Position(100, 0), position);
    }
}
