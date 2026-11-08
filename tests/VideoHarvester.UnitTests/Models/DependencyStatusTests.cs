using VideoHarvester.Models;

namespace VideoHarvester.UnitTests.Models;

public class DependencyStatusTests
{
    [Fact]
    public void IsAvailableRaisesPropertyChanged()
    {
        var status = new DependencyStatus { Name = "Tool", Icon = "icon" };
        string? changedProperty = null;
        status.PropertyChanged += (_, args) => changedProperty = args.PropertyName;

        status.IsAvailable = true;

        Assert.Equal(nameof(DependencyStatus.IsAvailable), changedProperty);
    }
}
