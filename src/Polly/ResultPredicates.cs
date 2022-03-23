using System.Collections.Generic;
using System.Linq;

namespace Polly
{
    // Useful notes: 
    
    // 1. It seems that they didn't initialize _predicates collection inside of ctor to not take memory.
    // Instead they do on-demand initialization in Add() method.
    // BUT as a trade-off they have to check _predicates for null in the rest of the methods.
    
    // 2. This class adheres to 'First class collections' principle.
    
    /// <summary>
    /// A collection of predicates used to define whether a policy handles a given <typeparamref name="TResult"/> value.
    /// </summary>
    public class ResultPredicates<TResult>
    {
        private List<ResultPredicate<TResult>> _predicates;

        internal void Add(ResultPredicate<TResult> predicate)
        {
            _predicates ??= new List<ResultPredicate<TResult>>(); // The ?? pattern here is sufficient; only a deliberately contrived example would lead to the same PolicyBuilder instance being used in a multi-threaded way to define policies simultaneously on multiple threads.

            _predicates.Add(predicate);
        }

        /// <summary>
        /// Returns a bool indicating whether the passed <typeparamref name="TResult"/> value matched any predicates.
        /// </summary>
        /// <param name="result">The <typeparamref name="TResult"/> value to assess against the predicates.</param>
        public bool AnyMatch(TResult result)
        {
            if (_predicates == null) return false;

            return _predicates.Any(predicate => predicate(result));
        }

        /// <summary>
        /// Specifies that no result-handling filters are applied or are required.
        /// </summary>
        public static readonly ResultPredicates<TResult> None = new();
    }
}