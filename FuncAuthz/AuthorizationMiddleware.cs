using System.Security.Claims;
using FuncAuthz.Extensions;
using FuncAuthz.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Options;

namespace FuncAuthz;

internal class AuthorizationMiddleware(
    ITokenService tokenService,
    IHttpContextAccessor? httpContextAccessor = null,
    IOptions<Microsoft.AspNetCore.Authorization.AuthorizationOptions>? authorizationOptions = null,
    IAuthorizationService? authorizationService = null)
    : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext()!;

        if (httpContextAccessor is not null)
            httpContextAccessor.HttpContext = httpContext;

        context.Items["User"] = new ClaimsPrincipal();

        var request = httpContext.Request;
        var authorizationHeader = request.Headers.Authorization;
        var authorizationHeaderValue = authorizationHeader.FirstOrDefault();

        // Get the token from the request header
        var token = authorizationHeaderValue?.Replace("Bearer ", "");

        var claims = await tokenService.ValidateTokenAsync(token);

        var methodInfo = context.GetTargetFunctionMethod();
        var authorizeAttribute = AttributeExtension.GetAttribute<AuthorizeAttribute>(methodInfo);
        var anonymousAttribute = AttributeExtension.GetAttribute<AllowAnonymousAttribute>(methodInfo);

        if (HasAuthorizeEffect(authorizeAttribute, anonymousAttribute))
        {
            if (claims is null)
            { 
                await new UnauthorizedResult().ExecuteResultAsync(new ActionContext
                {
                    HttpContext = httpContext
                });
            }
            else
            {
                context.Items["User"] = claims;
                httpContext.User = claims;
                httpContext.Items["User"] = claims;
                
                var roles = ParseRoles(authorizeAttribute.GetValueOrDefault().attribute?.Roles);
                if (roles.Any())
                {
                    if (!UserIsInRole(claims, roles))
                    {
                        await new ForbidResult().ExecuteResultAsync(new ActionContext
                        {
                            HttpContext = httpContext
                        });
                        return;
                    }
                }

                // Check if the user is in the required policy
                var policies = ParsePolicies(authorizeAttribute.GetValueOrDefault().attribute?.Policy);
                if (policies.Any())
                {
                    if (!UserIsHasPolicy(claims, policies))
                    {
                        await new StatusCodeResult(StatusCodes.Status403Forbidden).ExecuteResultAsync(new ActionContext
                        {
                            HttpContext = httpContext
                        });
                        return;
                    }
                }

                if (authorizeAttribute!.Value.attribute is IAuthorizationFilter authorizationFilter)
                {
                    var routeData = httpContext.GetRouteData();

                    //Get action descriptor and filters
                    var endpoint = httpContext.GetEndpoint();
                    var actionDescriptor = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();
                    var filters = endpoint?.Metadata.GetMetadata<IEnumerable<IFilterMetadata>>();

                    var authorizationContext = new AuthorizationFilterContext(new ActionContext(httpContext, routeData,
                        actionDescriptor ?? new ActionDescriptor()), filters?.ToList() ?? []);
                    
                    authorizationFilter.OnAuthorization(authorizationContext);
                    var result = authorizationContext.Result;

                    if (result is ForbidResult)
                    {
                        await new StatusCodeResult(StatusCodes.Status403Forbidden).ExecuteResultAsync(new ActionContext
                        {
                            HttpContext = httpContext
                        });
                        return;
                    }

                    if (result is not null)
                    {
                        var objectResult = result as ObjectResult;
                        var statusCodeResult = result as StatusCodeResult;

                        await new ObjectResult(objectResult?.Value) { StatusCode = objectResult?.StatusCode ?? statusCodeResult?.StatusCode }
                            .ExecuteResultAsync(new ActionContext
                            {
                                HttpContext = httpContext,
                            });
                        return;
                    }
                }

                await next(context);
            }
        }
        else
        {
            context.Items["User"] = claims!;
            httpContext.User = claims!;
            httpContext.Items["User"] = claims!;

            await next(context);
        }
    }

    private static bool HasAuthorizeEffect((AuthorizeAttribute? attribute, AttributeTargets attributeTargets, int prentLevel)? authorize,
        (AllowAnonymousAttribute? attribute, AttributeTargets attributeTargets, int prentLevel)? anonymous)
    {
        if (authorize is null)
            return false;

        if (anonymous is null)
            return true;

        switch (authorize.GetValueOrDefault().attributeTargets)
        {
            case AttributeTargets.Class when
                anonymous.GetValueOrDefault().attributeTargets == AttributeTargets.Method:
                return false;
            case AttributeTargets.Method when
                anonymous.GetValueOrDefault().attributeTargets == AttributeTargets.Class:
            case AttributeTargets.Class when
                anonymous.GetValueOrDefault().attributeTargets == AttributeTargets.Class:
            case AttributeTargets.Method when
                (anonymous.GetValueOrDefault().attributeTargets == AttributeTargets.Method &&
                 authorize.GetValueOrDefault().prentLevel <= anonymous.GetValueOrDefault().prentLevel):
                return true;
        }

        if (authorize.GetValueOrDefault().prentLevel == anonymous.GetValueOrDefault().prentLevel)
        {
            return authorize.GetValueOrDefault().attributeTargets switch
            {
                AttributeTargets.Method when anonymous.GetValueOrDefault().attributeTargets == AttributeTargets.Class =>
                    true,
                AttributeTargets.Class when anonymous.GetValueOrDefault().attributeTargets == AttributeTargets.Method =>
                    false,
                _ => true
            };
        }

        return authorize.GetValueOrDefault().prentLevel < anonymous.GetValueOrDefault().prentLevel;
    }

    private static List<string> ParseRoles(string? roles)
    {
        if (roles is null)
            return [];

        return roles.Contains(",") ? roles.Split(",").ToList() : [roles];
    }

    private static List<string> ParsePolicies(string? policies)
    {
        if (policies is null)
            return [];

        return policies.Contains(",") ? policies.Split(",").ToList() : [policies];
    }

    private static bool UserIsInRole(ClaimsPrincipal user, IEnumerable<string> roles)
    {
        return roles.Any(user.IsInRole);
    }

    private bool UserIsHasPolicy(ClaimsPrincipal user, IEnumerable<string> policies)
    {
        if (authorizationOptions is null || authorizationService is null)
            return false;

        return policies
            .Select(policyName => authorizationOptions.Value.GetPolicy(policyName))
            .OfType<AuthorizationPolicy>()
            .Select(policy => authorizationService
                .AuthorizeAsync(user, policy))
            .Any(result => result.Result.Succeeded);
    }
}
