using System;
using FluentAssertions;
using Polly;
using Polly.Retry;
using Xunit;

namespace Sandbox.Polly.Retry
{
    public class RetryPolicyHandlesSpecifiedException
    {
        [Fact]
        public void Retry_policy_handles_specified_exception()
        {
            var executedTimes = 0;
            var exceptionThrownTimes = 0;

            // Many faults are transient and may self-correct after a short delay
            // Solution: Allows configuring automatic retries

            // we just created a builder which contains info what Exception to handle ?

            // The overall number of attempts that may be made to execute the action is one plus the number of retries configured
            // For example, if the policy is configured .Retry(3), up to four attempts are made: the initial attempt, plus up to three retries

            var policyBuilder = 
                Policy
                    .Handle<InvalidOperationException>()
                    .Or<OperationCanceledException>()
                    .Or<ArgumentException>(ex => ex.ParamName == "name");
            
            // now we create a policy based on builder
            var retryPolicy = policyBuilder.Retry(2);

            // Successful scenario

            retryPolicy.Execute(() =>
            {
                executedTimes++;
            });

            executedTimes.Is(1);
            
            // Throw once
            executedTimes = 0;

            retryPolicy.Execute(() =>
            {
                ++executedTimes;

                if (executedTimes is 1)
                {
                    exceptionThrownTimes++;
                    throw new ArgumentException("", "name");
                }
            });

            executedTimes.Is(2);
            exceptionThrownTimes.Is(1);

            // Always throwing

            executedTimes = 0;
            exceptionThrownTimes = 0;

            new Action(() =>
            {
                retryPolicy.Execute(() =>
                {
                    ++executedTimes;
                    ++exceptionThrownTimes;
                    throw new InvalidOperationException();
                });
            }).Should().Throw<InvalidOperationException>("Because we exited our 2 retry attempts");

            executedTimes.Is(3);
            exceptionThrownTimes.Is(3);
        }
    }
}