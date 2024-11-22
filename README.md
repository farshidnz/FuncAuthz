
# FuncAuthz  

<p align="center">
  <img src="FuncAuthz/assets/Funcauthz.png" alt="FuncAuthz Logo" width="200">
</p>

**FuncAuthz** is a .NET package designed to provide authentication and authorization for **Azure Function Apps** using **JWT tokens**. This package simplifies the process of securing your Azure Functions by integrating JWT token validation and role-based access control.  

---

## Features  

- **JWT token validation**  
- **Role-based access control**  
- **Policy-based authorization**  
- **Middleware integration for Azure Functions**  

---

## Installation  

To install FuncAuthz, add the package to your project using NuGet:  

```bash
dotnet add package FuncAuthz
```

---

## Usage  

### Configuration  

#### 1. Configure Services  

In your **`Program.cs`** or **`Startup.cs`**, configure the authentication and authorization services:  

```csharp
using System.Text;
using FuncAuthz.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(builder =>
    {
        builder.AddAuthentication()
            .AddJwtBearer(new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = true,
                ValidIssuer = "Issuer",
                RequireExpirationTime = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes("A secure key that's shared between AspNetCore and Azure Functions")),
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            });
    })
.Build();

host.Run();
```

#### 2. Add Middleware  

Ensure the `AuthorizationMiddleware` is added to the pipeline:  

```csharp
builder.UseMiddleware<AuthorizationMiddleware>();
```

---

### Applying Authorization  

#### 1. Authorize Function  

Use the `[Authorize]` attribute to secure your Azure Functions:  

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.AspNetCore.Authorization;
using System.Net;

public class MyFunction
{
    [Function("MyFunction")]
    [Authorize(Roles = "Admin")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req,
        FunctionContext executionContext)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync("Hello, authorized user!");
        return response;
    }
}
```

#### 2. Allow Anonymous Access  

Use the `[AllowAnonymous]` attribute to allow anonymous access to specific functions:  

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.AspNetCore.Authorization;
using System.Net;

public class MyFunction
{
    [Function("MyFunction")]
    [AllowAnonymous]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req,
        FunctionContext executionContext)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync("Hello, anonymous user!");
        return response;
    }
}
```

---

## Contributing  

Contributions are welcome! Please open an issue or submit a pull request on GitHub.  

---

## License  

This project is licensed under the MIT License. See the `LICENSE` file for details.  

---

## Acknowledgements  

- **Microsoft IdentityModel**  
- **Azure Functions**  

---

## Contact  

For any questions or feedback, please contact the project maintainers.  

---  
