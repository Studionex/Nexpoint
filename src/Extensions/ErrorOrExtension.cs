
using Nexpoint.Models;
namespace Nexpoint.Extensions;

public static class ErrorOrExtension
{

    public static ErrorOr<TOut> Map<TIn, TOut>(
        this ErrorOr<TIn> errorOr,
        Func<TIn, TOut> mapper
        )
    {
        return errorOr.Match(
            value => mapper(value),
            errors => (ErrorOr<TOut>)errors
        );
    }



    public static OkResult<TOut> Ok<TIn, TOut>(
        this ErrorOr<TIn> errorOr,
        Func<TIn, TOut> mapper
    )
    {
        ErrorOr<TOut> mappedResult = errorOr.Map(mapper);
        return new OkResult<TOut>(mappedResult);
    }

    public static CreatedResult<TOut> Created<TIn, TOut>(
        this ErrorOr<TIn> errorOr,
        Func<TIn, TOut> mapper,
        string? location = null
    )
    {
        ErrorOr<TOut> mappedResult = errorOr.Map(mapper);
        return new CreatedResult<TOut>(mappedResult, location);
    }
    public static NoContentResult NoContent<TIn>(
        this ErrorOr<TIn> errorOr
    )
    {
        return new NoContentResult(errorOr.IsError ? errorOr.Errors : null);
    }
}
