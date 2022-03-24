using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions.Extensions;
using Polly;
using Xunit;
using Xunit.Abstractions;

namespace Sandbox.Polly.Tests
{
    public class BulkHeadTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public BulkHeadTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task Bulkhead_intro()
        {
            // Restrict executions through the policy to a maximum of twelve concurrent actions.
            
            var calledTimes = 0;
            var bulkheadRejectsExecutedTimes = 0;
            
            var bulkheadPolicy = 
                Policy.BulkheadAsync(5, context =>
                {
                    bulkheadRejectsExecutedTimes++;
                    return Task.CompletedTask;
                });
            
            bulkheadPolicy.BulkheadAvailableCount.Is(5);

            for (var i = 0; i < 100; i++)
                bulkheadPolicy.ExecuteAsync(async token =>
                {
                    calledTimes++;
                    await Task.Delay(1.Seconds(), token);
                }, CancellationToken.None);
            
            bulkheadPolicy.BulkheadAvailableCount.Is(0);
            calledTimes.Is(5);
            bulkheadRejectsExecutedTimes.Is(95);


            // give time for actions to complete
            await Task.Delay(1.5.Seconds());

            bulkheadPolicy.BulkheadAvailableCount.Is(5);
        }
    }
}