using System.Security.Claims;
using Microsoft.Azure.Functions.Worker.Http;

namespace FuncAuthz.Extensions;

public static class HttpRequestDataExtensions
{
    public static ClaimsPrincipal GetUser(this HttpRequestData req)
    {
        return req.FunctionContext.GetUser();
    }
}