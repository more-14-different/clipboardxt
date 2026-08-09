using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class FileDialogJumpHelperStrategyTests
{
    [Fact]
    public void BuildCustomStrategyOrder_PutsPinnedStrategyFirst()
    {
        var rule = new CustomFileDialogRule
        {
            StrategyOrder = ["shell_inject", "address_bar", "wps_chain"],
            PinnedStrategy = "wps_chain",
        };

        var order = FileDialogJumpHelper.BuildCustomStrategyOrder(rule);

        Assert.Equal(["wps_chain", "shell_inject", "address_bar"], order);
    }

    [Fact]
    public void BuildCustomStrategyOrder_RemovesCaseInsensitiveDuplicates()
    {
        var rule = new CustomFileDialogRule
        {
            StrategyOrder = ["address_bar", "ADDRESS_BAR", "sys_listview"],
        };

        var order = FileDialogJumpHelper.BuildCustomStrategyOrder(rule);

        Assert.Equal(["address_bar", "sys_listview"], order);
    }

    [Fact]
    public void BuildCustomStrategyOrder_IgnoresEmptyStrategies()
    {
        var rule = new CustomFileDialogRule
        {
            StrategyOrder = ["", "address_bar", "", "wps_chain"],
        };

        var order = FileDialogJumpHelper.BuildCustomStrategyOrder(rule);

        Assert.Equal(["address_bar", "wps_chain"], order);
    }

    [Fact]
    public void BuildCustomStrategyOrder_UsesDefaultsWhenOrderIsEmpty()
    {
        var rule = new CustomFileDialogRule();

        var order = FileDialogJumpHelper.BuildCustomStrategyOrder(rule);

        Assert.Equal(CustomFileDialogStore.DefaultStrategyOrder, order);
    }
}
