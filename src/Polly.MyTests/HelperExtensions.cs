using System.Text;
using FluentAssertions;

namespace Sandbox.Polly
{
    public static class HelperExtensions
    {
        public static void Is<T>(this T current, T expected , string because = null)
        {
            current.Should().Be(expected, because ?? string.Empty);
        }
        
    }
}