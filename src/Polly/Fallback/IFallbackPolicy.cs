namespace Polly.Fallback
{
    /// <summary>
    /// Defines properties and methods common to all Fallback policies.
    /// </summary>

    // TODO: How all this kind of  interface interact the process ?
    
    public interface IFallbackPolicy : IsPolicy
    {
    }

    /// <summary>
    /// Defines properties and methods common to all Fallback policies generic-typed for executions returning results of type <typeparamref name="TResult"/>.
    /// </summary>
    public interface IFallbackPolicy<TResult> : IFallbackPolicy
    {
    }
}
