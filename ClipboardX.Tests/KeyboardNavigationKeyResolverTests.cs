using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class KeyboardNavigationKeyResolverTests
{
    [Theory]
    [InlineData(0x48u, -3)]
    [InlineData(0x4Au, 1)]
    [InlineData(0x4Bu, -1)]
    [InlineData(0x4Cu, 3)]
    public void ResolveHjklDelta_MapsNavigationKeys(uint key, int expected)
    {
        Assert.Equal(expected, KeyboardNavigationKeyResolver.ResolveHjklDelta(key));
    }

    [Fact]
    public void ResolveHjklDelta_RejectsOtherKeys()
    {
        Assert.Null(KeyboardNavigationKeyResolver.ResolveHjklDelta(0x51));
    }
}
