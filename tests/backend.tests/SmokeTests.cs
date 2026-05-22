using FluentAssertions;
using Xunit;

namespace backend.tests
{
    public class SmokeTests
    {
        [Fact]
        public void TestInfrastructure_ShouldWork()
        {
            bool itWorks = true;
            itWorks.Should().BeTrue();
        }
    }
}
