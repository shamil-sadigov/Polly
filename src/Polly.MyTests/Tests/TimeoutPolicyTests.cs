using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Polly;
using Polly.Timeout;
using Xunit;

namespace Sandbox.Polly.Tests
{
    public class TimeoutPolicyTests
    {
        [Fact]
        public async Task OPTIMISTIC_timeout_throws_exception_if_timeout_passed()
        {
            // To ensure the caller never has to wait beyond the configured timeout
            bool executed = false;
            bool onTimeoutCalled = false;
            
            var timeoutPolicy = 
                Policy.TimeoutAsync(TimeSpan.FromSeconds(1), 
                    onTimeoutAsync: (context, timeSpan, task) =>
                {
                    // do logging here

                    onTimeoutCalled = true;
                    return Task.CompletedTask;
                });

            await timeoutPolicy.Invoking(x=> x.ExecuteAsync(async (token) =>
            {
                // long running operation
                await Task.Delay(TimeSpan.FromSeconds(5), token);
            
                executed = true;
            }, CancellationToken.None))
            .Should()
            .ThrowAsync<TimeoutRejectedException>("Becuase our policy can wait only 1 second");
            
            executed.Is(false);
            onTimeoutCalled.Is(true);
        }   
        
    }
}