using ClampDown.Core.HelperIpc;

namespace ClampDown.Tests;

public class HelperSessionFactoryTests
{
    [Fact]
    public void CreateForCurrentProcess_ReturnsSessionWithExpectedFields()
    {
        var session = HelperSessionFactory.CreateForCurrentProcess();

        Assert.False(string.IsNullOrWhiteSpace(session.PipeName));
        Assert.False(string.IsNullOrWhiteSpace(session.AuthorizationToken));
        Assert.True(session.AllowedClientProcessId > 0);
        Assert.Contains(session.AllowedClientProcessId.ToString(), session.PipeName, StringComparison.Ordinal);
    }
}
