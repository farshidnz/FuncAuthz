using Microsoft.Azure.Functions.Worker;

namespace FuncAuthz.Extensions;

public static class FunctionsWorkerApplicationBuilderExtension
{

    public static AuthenticationBuilder AddAuthentication(this IFunctionsWorkerApplicationBuilder builder)
    {
        return new AuthenticationBuilder(builder);
    }
}
