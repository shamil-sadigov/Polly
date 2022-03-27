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