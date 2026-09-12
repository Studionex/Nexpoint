using Microsoft.AspNetCore.Routing;
using Nexpoint.Helpers;

namespace Nexpoint;

public abstract class NexEndpoint
{
  public virtual List<Error> PossibleErrors { get; } = [];
  public virtual string[]? RequiredPermissions { get; } = null;
  public virtual string? Description { get; } = null;
  public virtual bool AllowAnonymous { get; } = false;
  public abstract string Route { get; }
  public static IResult Problem(
      List<Error> errors,
      HttpContext httpContext)
  {
    if (errors.Count == 0)
      return Results.Problem();

    if (errors.All(e => e.Type == ErrorType.Validation))
      return ValidationProblem(errors);

    httpContext?.Items["Errors"] = errors;

    return Problem(errors[0]);
  }

  private static IResult Problem(Error error)
  {
    var statusCode = Helper.GetStatusCode(error.Type);
    return Results.Problem(
        statusCode: statusCode,
        title: error.Code,
        detail: error.Description,
        extensions: error.Metadata?.AsEnumerable()
    );
  }

  private static IResult ValidationProblem(List<Error> errors)
  {
    var dic = errors
        .GroupBy(e => e.Code)
        .ToDictionary(
            g => g.Key,
            g => g.Select(e => e.Description).ToArray()
        );

    return Results.ValidationProblem(dic);
  }

  internal IEndpointRouteBuilder Api { get; private set; } = default!;

  public void SetApi(IEndpointRouteBuilder api)
  {
    Api = api;
  }
  public abstract void Register();

}

