using System;
using System.Data;
using System.Threading;
using FluentAssertions;
using Microsoft.VisualBasic.CompilerServices;
using Polly;
using Xunit;
using Xunit.Abstractions;

namespace Sandbox.Polly.Tests
{
    public class HttpResult
    {
        public bool IsSuccessful { get; set; }
        
        public string Content { get; set; }
    }
    
    
    public class FallbackPolicy
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public FallbackPolicy(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void If_specified_exception_is_thrown_or_result_is_returned_then_fallbackAction_is_invoked()
        {
            // provide a substitute value (or substitute action to be actioned)
            // in the event of failure

            string expectedExceptionMessage = string.Empty;
            string expectedResultMessage = string.Empty;
            int onFallbackCalled = 0;
            
            var fallbackPolicy =
                Policy<HttpResult>
                    .Handle<InvalidOperationException>()
                    .OrResult(httpRes => !httpRes.IsSuccessful) // if result is not successful the fallback will be invoked
                    .Fallback(
                        fallbackAction:() => new HttpResult()
                        {
                            Content = "Our custom fallback response"
                        }, 
                        onFallback: result =>
                        {
                            // do some logging here
                            
                            onFallbackCalled++;
                            if (result.Exception is not null)
                                expectedExceptionMessage = result.Exception.Message;

                            if (result.Result is not null)
                                expectedResultMessage = result.Result.Content;
                        });

            
            {
                // 1 Get fallback result when expected exception is thrown

                var httpResult = fallbackPolicy.Execute(
                    (ctx, token )=> throw new InvalidOperationException("Baby"), 
                    new Context(), CancellationToken.None);
            
                httpResult.Content.Is("Our custom fallback response");
                expectedExceptionMessage.Is("Baby");
                onFallbackCalled.Is(1);
            }
            
            onFallbackCalled.Reset();
            
            {
                // 2 Get fallback result when expected result is returned
                
                var httpResult = fallbackPolicy.Execute(() => new HttpResult()
                {
                    Content = "Hello content",
                    IsSuccessful = false
                });
            
                httpResult.Content.Is("Our custom fallback response");
                expectedResultMessage.Is("Hello content");
                onFallbackCalled.Is(1);
            }

            onFallbackCalled.Reset();

            {
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
        
        [Fact]
        public void Fallback_for_void_returning_calls()
        {
            bool fallBackCalled = false;
            
            var fallbackPolicy = Policy
                    .Handle<InvalidOperationException>()
                    .Fallback(() =>
                    {
                        // logging
                        // or other staff here
                        
                        fallBackCalled = true;
                    });


            fallbackPolicy.Execute(() => throw new InvalidOperationException());
            
            fallBackCalled.Is(true);
        }
        
        [Fact]
        public void Policy_doesnt_catch_unspecified_exception()
        {
            var fallbackPolicy =
                Policy<HttpResult>
                    .Handle<InvalidOperationException>()
                    .OrResult(httpRes => !httpRes.IsSuccessful)
                    .Fallback(() => new HttpResult()
                    {
                        Content = "Our custom fallback response"
                    });

            
            fallbackPolicy.Invoking(
                    x => x.Execute((ctx, token )=> throw new DBConcurrencyException("Baby"), 
                    new Context(), 
                    CancellationToken.None))
                .Should()
                .Throw<DBConcurrencyException>("Becuase we didnt' specified this type of exception in policy");
        }
        
    }
}