namespace Nexpoint.Models;

public static class NexResult
{
  public static OkResult<T> Ok<T>(T value) => new OkResult<T>(value);

  public static CreatedResult<T> Created<T>(T value, string? location = null) => new CreatedResult<T>(value, location);

  public static NoContentResult NoContent(List<Error>? errors) => new NoContentResult(errors);

}
