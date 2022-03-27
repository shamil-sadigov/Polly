using System;
using System.Net.Http;
using System.Threading;
using Polly;
using Sandbox.Polly.Tests;
using Xunit;

namespace Sandbox.Polly.Retry
{
    public class If_specified_exception_is_thrown_or_result_is_returned_then_result_from_fallbackAction_is_returned
    {
        [Fact]
        public void Go()
        {
            // provide a substitute value (or substitute action to be actioned)
            // in the event of failure

            string expectedExceptionMessage = string.Empty;
            string expectedResultMessage = string.Empty;
            int onFallbackCalled = 0;
            
            var fallbackPolicy =
                Policy<HttpResult>
                    .Handle<HttpRequestException>() // If this Exeption will be thrown the fallback will be invoked
                    .OrResult(httpRes => !httpRes.IsSuccessful) // if result is not successful the fallback will be invoked
                    .Fallback(
                        fallbackProvider:() =>
                        {
                            var fallbackResult = new HttpResult()
                            {
                                Content = "Fallback result"
                            };

                            return fallbackResult;
                        }, 
                        // called before 'fallbackAction'
                        onFallback: (DelegateResult<HttpResult> delegateResult) =>
                        {
                            // do some logging here
                            onFallbackCalled++;
                            
                            // Typicall, either Exception or Result has value
                            expectedExceptionMessage = delegateResult.Exception?.Message;
                            expectedResultMessage = delegateResult.Result?.Content;
                        });

            
            {
                onFallbackCalled = 0;
                
                // 1 Get fallback result when expected exception is thrown

                var httpResult = fallbackPolicy.Execute(
                    (ctx, token )=> throw new HttpRequestException("Baby"), 
                    new Context(), CancellationToken.None);
            
                httpResult.Content.Is("Fallback result");
                expectedExceptionMessage.Is("Baby");
                onFallbackCalled.Is(1);
            }
            
            
            {
                onFallbackCalled = 0;
                // 2 Get fallback result when expected result is returned
                
                var httpResult = fallbackPolicy.Execute(() => new HttpResult()
                {
                    Content = "Hello content",
                    IsSuccessful = false
                });
            
                httpResult.Content.Is("Fallback result");
                expectedResultMessage.Is("Hello content");
                onFallbackCalled.Is(1);
            }
            
            {
                onFallbackCalled = 0;
                // 3 Get successful result
                var httpResult = fallbackPolicy.Execute(() => new HttpResult()
                {
                    Content = "Hello content",
                    IsSuccessful = true
                });
            
                httpResult.Content.Is("Hello content");
                onFallbackCalled.Is(0);
            }

        }
    }
}