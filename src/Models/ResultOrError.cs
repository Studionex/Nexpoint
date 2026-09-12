
using Microsoft.AspNetCore.Http.HttpResults;

namespace Nexpoint.Models;

public interface IResultOrError<T> : IResult
{
}
public interface IHasStatusCode
{
  public static abstract int StatusCode { get; }
}
public abstract class ResultOrError<T> : IResultOrError<T>
{
  public T? Value { get; }
  public List<Error> Errors { get; } = [];
  public bool IsError => Errors.Count > 0;

  public ResultOrError(T? value, List<Error>? errors)
  {
    Value = value;
    if (errors != null)
    {
      Errors = errors;
    }
  }


  public abstract Task ExecuteAsync(HttpContext httpContext);
  public static Task ExecuteErrorAsync(List<Error> errors, HttpContext httpContext)
  {
    var problemResult = NexEndpoint.Problem(errors, httpContext);
    return problemResult.ExecuteAsync(httpContext);
  }
}

public class OkResult<T>(ErrorOr<T> errorOr) : ResultOrError<T>(errorOr.Value, errorOr.IsError ? errorOr.Errors : []), IHasStatusCode
{
  public static int StatusCode => 200;

  public override Task ExecuteAsync(HttpContext httpContext)
  {

    if (IsError)
    {
      return ExecuteErrorAsync(Errors, httpContext);

    }



    httpContext.Response.StatusCode = StatusCodes.Status200OK;
    return httpContext.Response.WriteAsJsonAsync(Value);

  }
}

public class CreatedResult<T> : ResultOrError<T>, IHasStatusCode
{
  public string? Location { get; }

  public static int StatusCode => StatusCodes.Status201Created;

  public CreatedResult(ErrorOr<T> errorOr, string? location = null) : base(errorOr.Value, errorOr.IsError ? errorOr.Errors : [])
  {
    if (location != null)
    {
      Location = location;
    }
  }
  public override Task ExecuteAsync(HttpContext httpContext)
  {
    if (IsError)
    {
      return ExecuteErrorAsync(Errors, httpContext);
    }
    httpContext.Response.StatusCode = StatusCodes.Status201Created;
    if (!string.IsNullOrEmpty(Location))
    {
      httpContext.Response.Headers.Location = Location;
    }
    else
    {
      string baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
      string fullUrl = $"{baseUrl}{httpContext.Request.Path}";
      string tid = Value?.GetType().GetProperty("Id")?.GetValue(Value)?.ToString() ?? string.Empty;
      httpContext.Response.Headers.Location = $"{fullUrl}/{tid}";
    }

    return httpContext.Response.WriteAsJsonAsync(Value);
  }
}

public class NoContentResult : IResult
{
  public List<Error> Errors { get; } = [];
  public bool IsError => Errors.Count > 0;

  public NoContentResult(List<Error>? errors)
  {
    if (errors != null)
    {
      Errors = errors;
    }
  }
  public Task ExecuteAsync(HttpContext httpContext)
  {
    if (IsError)
    {
      return ResultOrError<object>.ExecuteErrorAsync(Errors, httpContext);
    }

    httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
    return Task.CompletedTask;
  }
}

// Stream fileStream,
//         string? contentType,
//         string? fileDownloadName,
//         bool enableRangeProcessing,
//         DateTimeOffset? lastModified = null,
//         EntityTagHeaderValue? entityTag = null)


public class FileResult : IResult
{
  public bool ContentDispositionInline { get; set; }
  public bool EnableRangeProcessing { get; }
  public Stream? Value { get; }
  public List<Error> Errors { get; } = [];
  public bool IsError => Errors.Count > 0;

  public string? ContentType { get; }
  public string? FileDownloadName { get; }

  public FileResult(ErrorOr<Stream> errorOr, string? contentType = null, string? fileDownloadName = null, bool contentDispositionInline = true, bool enableRangeProcessing = true)
  {
    if (errorOr.IsError)
    {
      Errors = errorOr.Errors;
    }
    else
    {
      Value = errorOr.Value;
    }

    ContentDispositionInline = contentDispositionInline;
    EnableRangeProcessing = enableRangeProcessing;
    ContentType = contentType;
    FileDownloadName = fileDownloadName;
  }
  public Task ExecuteAsync(HttpContext httpContext)
  {
    if (IsError)
    {
      return ResultOrError<object>.ExecuteErrorAsync(Errors, httpContext);
    }
    if (ContentDispositionInline)
      httpContext.Response.Headers.ContentDisposition = "inline";
    return Results.File(Value!, ContentType, FileDownloadName, enableRangeProcessing: EnableRangeProcessing).ExecuteAsync(httpContext);


  }
}

