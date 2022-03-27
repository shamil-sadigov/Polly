using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Polly;
using Polly.CircuitBreaker;
using Xunit;
using Xunit.Abstractions;

namespace Sandbox.Polly.Tests
{
    public class CircuitBreakerTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public CircuitBreakerTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task Simple_circuit_breaker_flow()
        {
            int executedTimes = 0;
            bool onBreakCalled = false;
            bool onResetCalled  = false;
            bool onHalfOpenCalled = false;
            
            void OnBreak(Exception exception, TimeSpan span) => onBreakCalled = true;
            void OnReset() => onResetCalled = true;
            void OnHalfOpen () => onHalfOpenCalled = true;
            // Break the circuit after 2 times exceptions are thrown
            // and keep circuit broken for the specified duration
            // A circuit-breaker does not (unlike retry) absorb exceptions. All exceptions thrown by actions 
            // executed through the policy (both exceptions handled by the policy and not) are intentionally rethrown.


            CircuitBreakerPolicy circuitBreakerPolicy = 
                Policy.Handle<ArgumentException>(x => x.ParamName == "name")
                      .CircuitBreaker(2, TimeSpan.FromSeconds(3), OnBreak, OnReset, OnHalfOpen);
            
            circuitBreakerPolicy.CircuitState.Is(CircuitState.Closed);
            
            circuitBreakerPolicy.Invoking(x=> x.Execute(() =>
                {
                    executedTimes++;
                    throw new ArgumentException("", "name");
                }))
                .Should().Throw<ArgumentException>(
                    "Because circuitBreaker doesnt consume Exceptions, but rethrow instead");
                
            circuitBreakerPolicy.CircuitState.Is(CircuitState.Closed, "Because we still have one attempt");
            onBreakCalled.Is(false);

            circuitBreakerPolicy.Invoking(x=> x.Execute(() =>
                {
                    executedTimes++;
                    throw new ArgumentException("", "name");
                }))
                .Should().Throw<ArgumentException>();
            
            circuitBreakerPolicy.CircuitState.Is(CircuitState.Open, "Becuase we achieved threshold of 2 attempts");
            onBreakCalled.Is(true);
            onHalfOpenCalled.Is(false);

            circuitBreakerPolicy.Invoking(x=> x.Execute(() =>
                {
                    executedTimes++;
                    throw new ArgumentException("", "name");
                }))
                .Should()
                .Throw<BrokenCircuitException>()
                .WithMessage("The circuit is now open and is not allowing calls.");

            await Task.Delay(TimeSpan.FromSeconds(4));
            
            circuitBreakerPolicy.CircuitState.Is(CircuitState.HalfOpen, "Because 3 second break is passed");
            onResetCalled.Is(false);
            onHalfOpenCalled.Is(true);
            circuitBreakerPolicy.Execute(() =>
            {
                // suppose successful execution    
            });
            
            circuitBreakerPolicy.CircuitState.Is(CircuitState.Closed);
            onResetCalled.Is(true);
            
            executedTimes.Is(2);
            
            
            // An instance of CircuitBreakerPolicy maintains internal state to track failures 
            // across multiple calls through the policy: you must re-use the same CircuitBreakerPolicy 
            // instance for each execution through a call site, not create a fresh instance on each traversal of the code.
            
            
            // You may, further, share the same CircuitBreakerPolicy instance across multiple call sites, to cause them to break in common
            
            
            // A CircuitBreakerPolicy instance maintains internal state across calls to track failures, as described above. 
            // To do this in a thread-safe manner, it uses locking. Locks are held for the minimum time possible: while 
            // the circuit-breaker reads or recalculates state, but not while the action delegate is executing.
            
            // Note: All state-transition delegates are executed within the lock held by the circuit-breaker during 
            //     transitions of state. Without this, in a multi-threaded environment, the state-change 
            // by the delegate could fail to hold (it could be superseded by other events while the delegate is 
            //     executing). For this reason, it is recommended to avoid long-running/potentially-blocking
            // operations within a state-transition delegate. If you do execute blocking operations within 
            // a state-transition delegate, be aware that any blocking will block other actions through the policy.
            
        }

        
        [Fact]
        public void CircuitBreaker_states()
        {
/*
            CircuitState.Closed
            CircuitState.Open
            CircuitState.HalfOpen
            CircuitState.Isolated
*/
            // Isolated: The circuit has been manually broken (see below).
            // A code pattern such as below can be used to reduce the number of BrokenCircuitExceptions thrown while the circuit is open, 
            
            var breaker = 
                Policy.Handle<ArgumentException>(x => x.ParamName == "name")
                      .CircuitBreaker(2, TimeSpan.FromSeconds(3));
            
            if (breaker.CircuitState is not CircuitState.Open 
                and not CircuitState.Isolated)
            {
                breaker.Execute(() => { }); // place call
            }
            
            // Note that code such as this is not necessary; it is an option for high-performance scenarios. 
            // In general, it is sufficient to place the call breaker.Execute(...), and the breaker
            // will decide for itself whether the action can be executed
            
            // In a highly concurrent environment, the breaker state could change between evaluating the
            // if condition and executing the action. Equally, in the half-open state, only 
            // one execution will be permitted per break duration.
        }


        [Fact]
        public void Manually_break_circuit()
        {
            var circuitBreaker = Policy.HandleResult<StringBuilder>(x => x.Length > 1)
                .Or<InvalidOperationException>()
                .CircuitBreaker(2, TimeSpan.FromSeconds(2));
            
            // will place the circuit in to a manually open state. This can be used, for example, 
            // to isolate a downstream system known to be struggling, or to take it offline for maintenance
            
            circuitBreaker.Isolate();
            
            circuitBreaker.CircuitState.Is(CircuitState.Isolated);

            circuitBreaker.Invoking(x => x.Execute(() => new StringBuilder("Some value")))
                .Should()
                .Throw<IsolatedCircuitException>();

            
            // we should manually reset it 
            
            circuitBreaker.Reset();
            circuitBreaker.CircuitState.Is(CircuitState.Closed);

            
            bool called = false;
            
            circuitBreaker.Execute(() =>
            {
                called = true;
                return new StringBuilder("Some value");
                // don't return null otherwise NRE will be in HandleResult<> delegate line 141
            });
            
            called.Is(true);
        }
    }
}
