using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Polly;
using Polly.Timeout;
using Xunit;

namespace Sandbox.Polly.Timeout
{
    public sealed class OPTIMISTIC_timeout_throws_exception_if_timeout_passed
    {
        [Fact]
        public async Task Go()
        {
            // To ensure the caller never has to wait beyond the configured timeout
            bool executed = false;
            bool onTimeoutCalled = false;
            
            var timeoutPolicy = Policy.TimeoutAsync(
                TimeSpan.FromSeconds(1), 
                onTimeoutAsync: (context, timeSpan, task) =>
                {
                    // do logging here

                    onTimeoutCalled = true;
                    return Task.CompletedTask;
                });

            TimeoutRejectedException expectedException = default;
            
            try
            {
                await timeoutPolicy.ExecuteAsync(async (token) =>
                {
                    // long running operation
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                    executed = true;
                }, CancellationToken.None);
            }
            catch (TimeoutRejectedException e)
            {
                expectedException = e;
            }
            
            expectedException.Should().NotBeNull();
            
            executed.Is(false);
            onTimeoutCalled.Is(true);
        }
    }
}