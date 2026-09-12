# Nexpoint

Helpers for ASP.NET Core minimal APIs: endpoint registration, ErrorOr results, FluentValidation, and OpenAPI error examples.

Requires **.NET 10** and `Microsoft.AspNetCore.App`.

## Install

```bash
dotnet add package Nexpoint
```

## Setup

```csharp
builder.Services.AddNexpoint();

var app = builder.Build();
app.MapEndpointsFromAssembly();
```

`MapEndpointsFromAssembly` maps every `NexEndpoint` in the calling assembly under `/api`.

## Endpoint

```csharp
public class FakeEndpoint : NexEndpoint
{
  public override string Route => "/fake";

  public override List<Error> PossibleErrors =>
  [
    Error.Conflict("FakeConflict", "Already exists"),
  ];

  public override void Register()
  {
    this.MapPost<FakeCommand>(() => NexResult.Ok("ok"));
  }
}
```

Return `NexResult.Ok`, `NexResult.Created`, or `NexResult.NoContent`. From `ErrorOr<T>`, use `NexOk`, `NexCreated`, or `NexNoContent`.

## License

MIT
