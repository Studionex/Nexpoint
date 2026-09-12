using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Nexpoint.Models;
using System.Reflection;

namespace Nexpoint.Helpers;

public record ApiErrorResponse
{
  public required string Type { get; init; }
  public required string Title { get; init; }
  public required int Status { get; init; }
  public required string Detail { get; init; }
}
public static class Helper
{

  public static void DocumentErrors<TRequest>(List<Error>? errors, RouteHandlerBuilder builder) where TRequest : class
  {

    builder.Produces<ApiErrorResponse>(400);


    var errorsByStatusCode = errors?
        .GroupBy(err => GetStatusCode(err.Type))
        .ToList();

    foreach (var errorGroup in errorsByStatusCode ?? Enumerable.Empty<IGrouping<int, Error>>())
    {
      var statusCode = errorGroup.Key;
      builder.Produces<ApiErrorResponse>(statusCode);
    }
    builder.AddOpenApiOperationTransformer((operation, context, ct) =>
    {
      AddFluentValidationExamples<TRequest>(operation, context);
      AddPossibleErrorExamples(operation, errorsByStatusCode);
      return Task.CompletedTask;
    });

  }

  private static void AddFluentValidationExamples<TRequest>(
      OpenApiOperation operation,
      OpenApiOperationTransformerContext context)
      where TRequest : class
  {
    try
    {
      var extractor = context.ApplicationServices.GetService<ValidationErrorExtractor>();
      var validationErrors = extractor?.GetValidationErrors<TRequest>();
      if (validationErrors is not { Count: > 0 })
        return;

      if (operation.Responses is not { } responses ||
          !responses.TryGetValue("400", out var validationResponse) ||
          validationResponse.Content is not { } validationContents ||
          !validationContents.TryGetValue("application/json", out var validationContent) ||
          validationContent is null)
        return;

      var errorsObject = new JsonObject();
      foreach (var validationError in validationErrors)
      {
        var errorArray = new JsonArray();
        foreach (var message in validationError.Value)
          errorArray.Add(message);

        errorsObject[validationError.Key] = errorArray;
      }

      var validationErrorExample = $$"""
          {
              "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
              "title": "One or more validation errors occurred.",
              "status": 400,
              "errors": {{errorsObject.ToJsonString()}}
          }
          """;

      MergeExample(validationContent, "validationErrors", new OpenApiExample
      {
        Summary = "Validation errors example",
        Description = "An example of a validation error response with all validation errors for the Command",
        Value = JsonNode.Parse(validationErrorExample)
      });
    }
    catch
    {
      // FluentValidation docs are optional; PossibleErrors still load.
    }
  }

  private static void AddPossibleErrorExamples(
      OpenApiOperation operation,
      List<IGrouping<int, Error>>? errorsByStatusCode)
  {
    if (errorsByStatusCode is not { Count: > 0 } || operation.Responses is null)
      return;

    foreach (var errorGroup in errorsByStatusCode)
    {
      try
      {
        var statusCode = errorGroup.Key;
        var key = statusCode.ToString();

        if (!operation.Responses.TryGetValue(key, out var response) ||
            response.Content is not { } contents ||
            !contents.TryGetValue("application/json", out var content) ||
            content is null)
          continue;

        foreach (var error in errorGroup)
        {
          try
          {
            MergeExample(content, error.Code, new OpenApiExample
            {
              Summary = error.Code,
              Description = error.Description,
              Value = JsonNode.Parse($$"""
                  {
                      "type": "https://tools.ietf.org/html/rfc7231",
                      "title": "{{error.Code}}",
                      "status": {{statusCode}},
                      "detail": "{{error.Description}}"
                  }
                  """)
            });
          }
          catch
          {
            // Skip a single malformed example; keep the rest.
          }
        }
      }
      catch
      {
        // Skip one status group; keep the others.
      }
    }
  }

  private static void MergeExample(OpenApiMediaType content, string name, IOpenApiExample example)
  {
    var examples = content.Examples is Dictionary<string, IOpenApiExample> existing
        ? existing
        : content.Examples is { Count: > 0 }
            ? new Dictionary<string, IOpenApiExample>(content.Examples)
            : new Dictionary<string, IOpenApiExample>();

    examples[name] = example;
    content.Examples = examples;
  }

  public static int GetStatusCode(ErrorType errorType) => errorType switch
  {
    ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
    ErrorType.Forbidden => StatusCodes.Status403Forbidden,
    ErrorType.NotFound => StatusCodes.Status404NotFound,
    ErrorType.Conflict => StatusCodes.Status409Conflict,
    ErrorType.Validation => StatusCodes.Status400BadRequest,
    ErrorType.Failure => StatusCodes.Status500InternalServerError,
    _ => StatusCodes.Status500InternalServerError
  };
  public static (Type? responseType, int statusCode) GetDelegateResponseInfo(Delegate handler)
  {
    var returnType = handler.Method.ReturnType;

    if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
    {
      returnType = returnType.GetGenericArguments()[0];
    }

    var isResultOrError = returnType.GetInterfaces().Any(x =>
        x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IResultOrError<>));

    if (isResultOrError)
    {
      var responseType = returnType.GetGenericArguments()[0];
      if (responseType.GetInterfaces().Any(x => x == typeof(IHasStatusCode)))
        return (null, 500);
      var statusCodeProperty = returnType.GetProperty(nameof(IStatusCodeHttpResult.StatusCode),
          BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
      if (statusCodeProperty != null)
      {
        var statusCode = (int)(statusCodeProperty.GetValue(null) ?? 500);

        return (responseType, statusCode);
      }

    }
    else if (returnType == typeof(NoContentResult))
    {
      return (null, 204);
    }

    return (null, 500);
  }

  internal static RouteHandlerBuilder WrapWithAuthorization(RouteHandlerBuilder builder, string[] requiredPermissions)
  {
    builder.RequireAuthorization(p => p.RequireRole(requiredPermissions));
    return builder;
  }
}
