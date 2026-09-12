using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexpoint.Helpers;
namespace Nexpoint.Extensions;

public static class DepndencyInjectionExtensions
{
  public static IServiceCollection AddNexpoint(this IServiceCollection services)
  {
    services.TryAddScoped<ValidationErrorExtractor>();
    return services;
  }

  public static IEndpointRouteBuilder MapEndpointsFromAssembly(
      this IEndpointRouteBuilder api)
  {
    var assembly = Assembly.GetCallingAssembly();

    api = api.MapGroup("/api");
    var baseType = typeof(NexEndpoint);
    var types = assembly.GetTypes().Where(t =>
        t is { IsAbstract: false, IsInterface: false }
        && baseType.IsAssignableFrom(t));

    foreach (var type in types)
    {
      var instance = Activator.CreateInstance(type)
                     ?? throw new InvalidOperationException(
                         $"Could not create instance of {type.FullName}");

      if (instance is NexEndpoint endpoint)
      {
        endpoint.SetApi(api);
        endpoint.Register();
      }
    }

    return api;
  }
}
