#region

using System;
using System.Data;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Polly;
using Polly.Retry;
using Xunit;
using Xunit.Abstractions;

#endregion

namespace Sandbox.Polly.Tests
{
    public class RetryTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public RetryTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }
        
        [Fact]
        public void Retry_policy_dont_handle_unspecified_exceptions()
        {
            var thrown = false;
            var executedTimes = 0;

            var retryPolicy = Policy.Handle<OperationCanceledException>()
                .RetryForever();

            new Action(() =>
            {
                retryPolicy.Execute(() =>
                {
                    executedTimes++;

                    if (!thrown)
                    {
                        thrown = true;
                        throw new InvalidCastException();
                    }
                });
            }).Should().Throw<InvalidCastException>("Becuase we didn't specified this exception in builder");

            executedTimes.Is(1);
        }

        [Fact]
        public void Retry_calling_callback_on_each_failure()
        {
            var counter = 0;

            var retryPolicy =
                Policy.Handle<TaskCanceledException>()
                    .Or<DBConcurrencyException>()
                    .Retry(3, onRetry: (ex, retryCount, context) =>
                    {
                        // Add logic to be executed before each retry, such as logging

                        PrintExceptionType(retryCount, ex);
                    });

            retryPolicy.Execute(() =>
            {
                counter++;

                switch (counter)
                {
                    case 1 or 2:
                        throw new TaskCanceledException();
                    case 3:
                        throw new DBConcurrencyException();
                }
            });


            // Output
            // 1 TaskCanceledException
            // 2 TaskCanceledException
            // 3 DBConcurrencyException
        }

        
        
        [Fact]
        public void Wait_and_retry_in_exponential_backoff_testing()
        {
            // Inner exceptions of ordinary exceptions or AggregateException
            var policyBuilder = Policy.HandleInner<FileNotFoundException>();

            // A common retry strategy is exponential backoff

            var retryPolicy = policyBuilder.WaitAndRetry(new[]
            {
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(4),
                TimeSpan.FromSeconds(8),
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(30)
            }, onRetry: (exception, timeSpan, retryCount, context) =>
            {
                //  Can add logging here or whatever you want

                _testOutputHelper.WriteLine($"Retries with time {timeSpan} on {retryCount} times");
            });

            retryPolicy.Execute(() =>
            {
                _testOutputHelper.WriteLine(DateTime.Now.ToString("T"));
                throw new FileNotFoundException();
            });

            // 16:36:27
            // 16:36:28
            // 16:36:30
            // 16:36:33
            // Exception is thrown

            // OR you can do exponential backoff by calculation

            Policy
                .Handle<FileNotFoundException>()
                .WaitAndRetry(5, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        }


        [Fact]
        public void Retry_based_on_returned_result_NOT_exception()
        {
            int executedTimes = 0;
            int predicateCalledTimes = 0;
            
            var policyBuilder = Policy.HandleResult<OperationResult>(resultPredicate: operation =>
            {
                // while operation is not completed, continue to retry
                
                predicateCalledTimes++;
                return operation.Status != OperationStatus.Completed;
            })
                .OrResult(operation => operation.Status != OperationStatus.Canceled); 

            policyBuilder.Retry(10)
                .Execute(cancellationToken =>
                {
                    executedTimes++;
                    
                    // assume we do some operation and return a result
                    
                    var operation = new OperationResult()
                    {
                        Status = OperationStatus.Pending
                    };
                    
                    if (executedTimes == 5)
                    {
                        operation.Status = OperationStatus.Completed;
                    }
                    
                    return operation;

                }, CancellationToken.None);

            executedTimes.Is(5);
            predicateCalledTimes.Is(5);
        }
        
        
        [Fact]
        public void Retry_based_on_returned_result_or_exception()
        {
            int executedTimes = 0;
            int predicateCalledTimes = 0;

            var policyBuilder = Policy
                .Handle<CustomHttpException>()
                .OrResult<OperationResult>(resultPredicate: operation =>
                {
                    // while operation is not completed, continue to retry

                    predicateCalledTimes++;
                    return operation.Status != OperationStatus.Completed;
                });

            // And remember, if other exception are thrown then Policy will not handle them and just rethrow
            policyBuilder.Retry(10)
                .Execute(cancellationToken =>
                {
                    executedTimes++;
                    
                    // assume we do some operation and return a result
                    
                    var operation = new OperationResult()
                    {
                        Status = OperationStatus.Pending
                    };

                    operation.Status = executedTimes switch
                    {
                        3 => throw new CustomHttpException(),
                        5 => OperationStatus.Completed,
                        _ => operation.Status
                    };

                    return operation;

                }, CancellationToken.None);

            executedTimes.Is(5);
            predicateCalledTimes.Is(4);
        }

        
        [Fact]
        public void RetryAfter_when_response_specifies_how_much_time_to_wait()
        {
            // This can be used with cases of Async request-response pattern

            // Some systems specify how long to wait before retrying as part of the fault response returned.
            // This is typically expressed as a Retry-After header with a 429 response code.
            
            // This can be handled by using WaitAndRetry/Forever/Async(...)
            // overloads where the sleepDurationProvider takes the handled fault/exception as an input parameter
            // For example

            // THIS is bad exapmle because 
            // we want in sleedurationPRovider to get 'retry seconds' from HttpResponse and 
            // return TimeSpan from it, but since 'retry seconds' are assigned to context inside of 'onRetry'
            // and 'onRetry delegate' is called after 'sleeDurationProvider' delegate so we won't retrieve our 'retry seconds'
            // from throw exception

            goto solution;
            var thrown = false;

            var retryPolicy = Policy.Handle<CustomHttpException>()
                .WaitAndRetry(5,
                    sleepDurationProvider: (counter, context) => // called 1 after  Execute() throws
                    {
                        if (!context.Contains("Retry-After-Seconds"))
                            return TimeSpan.FromSeconds(2);

                        var seconds = context["Retry-After-Seconds"].ToString();

                        seconds.Should().Be("12");

                        _testOutputHelper.WriteLine("Will retry after " + seconds);

                        return TimeSpan.FromSeconds(int.Parse(seconds));
                    },
                    onRetry: (exception, _, _, context) => // called 2nd
                    {
                        if (exception is CustomHttpException httpException &&
                            httpException.TryGetHeaderValue("Retry-After-Seconds", out var seconds))
                        {
                            context.Add("Retry-After-Seconds", seconds);

                        }
                    });

            retryPolicy.Execute(() =>
            {
                if (!thrown)
                {
                    thrown = true;
                    throw new CustomHttpException();
                }
            });


            // Soltion

            solution:
            
            int sleepDurationProviderCalledTimes = 0;
            int executionCalledTimes = 0;
             thrown = false;

             RetryPolicy<CustomHttpResponse> retryPolicy2 = Policy
                 .Handle<CustomHttpException>()
                 
                 // this means that if method return false then retry won't be executed, so this is not that good
                 // because here we specify result that policy will handle
                 .OrResult<CustomHttpResponse>(response => response.StatusCode == HttpStatusCode.Accepted)
          
                 .WaitAndRetry(
                     5, 
                     sleepDurationProvider: (counter, delegateResult, context) =>
                     {
                         sleepDurationProviderCalledTimes++;
                         
                         CustomHttpResponse response = delegateResult.Result;
                         
                         // if CustomHttpException was  throw then response will not contain result
                         
                         if (response is not null && response.ContainsHeader("Retry-After-Seconds"))
                         {
                             string seconds = response.GetHeaderValue("Retry-After-Seconds");;
                              
                             seconds.Should().Be("2");

                             _testOutputHelper.WriteLine("Will retry after " + seconds);

                             return TimeSpan.FromSeconds(int.Parse(seconds));
                         }
                         
                         return TimeSpan.FromSeconds(1);
                     });

            retryPolicy2.Execute((token) =>
            {
                executionCalledTimes++;
                if (!thrown)
                {
                    thrown = true;
                    throw new CustomHttpException();
                }

                return sleepDurationProviderCalledTimes < 4 
                    ? new CustomHttpResponse(false) 
                    : new CustomHttpResponse(true);
                
            }, CancellationToken.None);

            _testOutputHelper.WriteLine(sleepDurationProviderCalledTimes.ToString()); // 5
            _testOutputHelper.WriteLine(executionCalledTimes.ToString()); // 6
        }


        [Fact]
        public void Retry_to_refresh_authorization()
        {
            bool doesnt_contain_auth_token_at_first_execution = default;
            bool contain_auth_token_at_second_execution = default;

            CustomHttpResponse customHttpResponse = 
                Policy.HandleResult<CustomHttpResponse>(x => x.StatusCode == HttpStatusCode.Unauthorized)
                    .Retry(retryCount: 1,
                           onRetry: (result, counter, context) =>
                           {
                               // onRetry is called only if unauhtoirzied response was received
                               // refresh your auth token by adding access_token and refresh_token to context
                               
                               // some request that get tokens
                               string tokens = "tokens";
                               
                               context.Add("auth-token", tokens);
                           })
                      .Execute((context) =>
                        {
                            if (context.Contains("auth-token"))
                            {
                                doesnt_contain_auth_token_at_first_execution = true;
                                return new CustomHttpResponse(true)
                                {
                                    StatusCode = HttpStatusCode.OK
                                };
                            }

                            contain_auth_token_at_second_execution = true;
                            return new CustomHttpResponse(true)
                            {
                                StatusCode = HttpStatusCode.Unauthorized
                            };

                        }, new Context());


            doesnt_contain_auth_token_at_first_execution.Is(true);
            contain_auth_token_at_second_execution.Is(true);
            
            customHttpResponse.StatusCode.Is(HttpStatusCode.OK);
           
            // Each call to .Execute(…) (or similar) through a retry policy maintains its own private state. 
            // A retry policy can therefore be re-used safely in a multi-threaded environment
        }
        
        
        
        
        

        private void PrintExceptionType(int coutner, Exception ex)
        {
            _testOutputHelper.WriteLine(coutner + " " + ex.GetType().Name);
        }

        public class CustomHttpException : Exception
        {
            public bool TryGetHeaderValue(string headerValue, out string value)
            {
                value = "2";
                return true;
            }
        }
        
        public class CustomHttpResponse
        {
            public HttpStatusCode StatusCode = HttpStatusCode.Accepted;
            
            private readonly bool _containsHeaderDefaultValue;

            public CustomHttpResponse(bool containsHeaderDefaultValue = true)
            {
                _containsHeaderDefaultValue = containsHeaderDefaultValue;
            }
            
            public bool ContainsHeader(string retryAfterSeconds)
            {
                return _containsHeaderDefaultValue;
            }

            public string GetHeaderValue(string retryAfterSeconds)
            {
                return "2";
            }
        }
        
        
        public class OperationResult
        {
            public OperationStatus Status { get; set; }
        }
        
        public enum OperationStatus
        {
            Pending, Completed, Canceled
        }
    }
}