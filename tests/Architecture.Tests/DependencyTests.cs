using FluentAssertions;
using Xunit;

namespace Architecture.Tests;

public sealed class DependencyTests
{
    [Fact]
    public void Domain_ReferencesOnlySharedKernel()
    {
        var references = typeof(Order.Domain.Order).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();
        references.Any(name => name is not null && (name.Contains("Infrastructure", StringComparison.Ordinal) || name.Contains("Application", StringComparison.Ordinal))).Should().BeFalse();
    }
}
