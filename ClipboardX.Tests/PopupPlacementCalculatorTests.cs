using System.Drawing;
using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class PopupPlacementCalculatorTests
{
    [Theory]
    [InlineData(10, 20, 11, 40, true)]
    [InlineData(0, 0, 0, 0, false)]
    [InlineData(10, 20, 10, 40, false)]
    [InlineData(10, 20, 11, 20, false)]
    public void HasUsableNativeCaretBounds_RequiresPositiveWidthAndHeight(
        int left,
        int top,
        int right,
        int bottom,
        bool expected)
    {
        Assert.Equal(
            expected,
            PopupPlacementCalculator.HasUsableNativeCaretBounds(left, top, right, bottom));
    }

    [Theory]
    [InlineData("Edit", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void HasUsableAutomationClassName_RejectsMissingClassName(
        string? className,
        bool expected)
    {
        Assert.Equal(expected, PopupPlacementCalculator.HasUsableAutomationClassName(className));
    }

    [Theory]
    [InlineData(1920, 1080, 1920, 1080, true)]
    [InlineData(1900, 1080, 1920, 1080, true)]
    [InlineData(1800, 1048, 1920, 1080, false)]
    [InlineData(0, 1080, 1920, 1080, false)]
    [InlineData(1920, 1080, 0, 0, false)]
    public void CoversForegroundWindow_UsesAreaThreshold(
        double candidateWidth,
        double candidateHeight,
        double foregroundWidth,
        double foregroundHeight,
        bool expected)
    {
        Assert.Equal(
            expected,
            PopupPlacementCalculator.CoversForegroundWindow(
                candidateWidth,
                candidateHeight,
                foregroundWidth,
                foregroundHeight));
    }

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
