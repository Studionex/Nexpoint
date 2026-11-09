using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nexpoint.Helpers;

namespace Nexpoint.Extensions;

public static class NexResultExtension
{
    public static RouteHandlerBuilder MapGet<TRequest>(this NexEndpoint endpoint, Delegate handler)
        where TRequest : class, IBaseRequest
        => endpoint.MapHttp<TRequest>(HttpMethod.Get, handler);

    public static RouteHandlerBuilder MapPost<TRequest>(this NexEndpoint endpoint, Delegate handler)
        where TRequest : class
        => endpoint.MapHttp<TRequest>(HttpMethod.Post, handler);

    public static RouteHandlerBuilder MapPut<TRequest>(this NexEndpoint endpoint, Delegate handler)
        where TRequest : class
        => endpoint.MapHttp<TRequest>(HttpMethod.Put, handler);

    public static RouteHandlerBuilder MapDelete<TRequest>(this NexEndpoint endpoint, Delegate handler)
        where TRequest : class
        => endpoint.MapHttp<TRequest>(HttpMethod.Delete, handler);

    private static RouteHandlerBuilder MapHttp<TRequest>(
        this NexEndpoint endpoint,
        string httpMethod,
        Delegate handler)
        where TRequest : class
    {
        var builder = endpoint.Api.MapVerb<TRequest>(
            httpMethod,
            endpoint.Route,
            handler,
            endpoint.PossibleErrors);

        ApplyAuthorization(endpoint, ref builder);

        var tag = GetTagFromRoute(endpoint.Route);
        builder = builder.WithTags(tag);

        builder = ApplyCredentialsDescription(endpoint, builder);

        return builder;
    }

    private static void ApplyAuthorization(NexEndpoint endpoint, ref RouteHandlerBuilder builder)
    {
        if (endpoint.AllowAnonymous)
        {
            builder.AllowAnonymous();
            return;
        }
        else
        {
            builder = Helper.WrapWithAuthorization(builder, endpoint.RequiredRoles, endpoint.RequiredPermissions);
        }
    }

    private static RouteHandlerBuilder ApplyCredentialsDescription(NexEndpoint endpoint, RouteHandlerBuilder builder)
    {
        var sections = new List<string>();

        if (!string.IsNullOrWhiteSpace(endpoint.Description))
            sections.Add(endpoint.Description.Trim());

        if (endpoint.RequiredPermissions is { Length: > 0 })
            sections.Add("Required permissions:\n" +
                string.Join("\n", endpoint.RequiredPermissions.Select(p => $"- {p}")));

        if (endpoint.RequiredRoles is { Length: > 0 })
            sections.Add("Required roles:\n" +
                string.Join("\n", endpoint.RequiredRoles.Select(p => $"- {p}")));

        return sections.Count == 0
            ? builder
            : builder.WithDescription(string.Join("\n\n", sections));
    }

    private static string GetTagFromRoute(string route)
    {
        if (string.IsNullOrWhiteSpace(route) || !route.StartsWith('/'))
            throw new ArgumentException($"Route must start with '/'. Invalid route: {route}");

        var parts = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : route;
    }

    private static RouteHandlerBuilder MapVerb<TRequest>(
        this IEndpointRouteBuilder api,
        string httpMethod,
        string pattern,
        Delegate handler,
        List<Error>? errors = null)
        where TRequest : class
    {
        RouteHandlerBuilder builder = httpMethod switch
        {
            HttpMethod.Get => api.MapGet(pattern, handler),
            HttpMethod.Post => api.MapPost(pattern, handler),
            HttpMethod.Put => api.MapPut(pattern, handler),
            HttpMethod.Delete => api.MapDelete(pattern, handler),
            _ => throw new NotSupportedException($"Unsupported HTTP method: {httpMethod}")
        };

        var (responseType, resStatusCode) = Helper.GetDelegateResponseInfo(handler);

        if (responseType != null)
            builder.Produces(resStatusCode, responseType);
        else if (resStatusCode == 204)
            builder.Produces(resStatusCode);

        Helper.DocumentErrors<TRequest>(errors, builder);

        return builder;
    }

    private static class HttpMethod
    {
        public const string Get = "GET";
        public const string Post = "POST";
        public const string Put = "PUT";
        public const string Delete = "DELETE";
    }
}
