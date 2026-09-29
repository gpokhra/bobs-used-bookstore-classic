using Amazon.Rekognition;
using Amazon.S3;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Bookstore.Data;
using Bookstore.Data.FileServices;
using Bookstore.Data.ImageResizeService;
using Bookstore.Data.ImageValidationServices;
using Bookstore.Data.Repositories;
using Bookstore.Domain;
using Bookstore.Domain.Addresses;
using Bookstore.Domain.Books;
using Bookstore.Domain.Carts;
using Bookstore.Domain.Customers;
using Bookstore.Domain.Offers;
using Bookstore.Domain.Orders;
using Bookstore.Domain.ReferenceData;
using Bookstore.Web.Helpers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using NLog;
using NLog.AWS.Logger;
using NLog.Config;
using NLog.Targets;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration — BookstoreConfiguration singleton backed by IConfiguration
// ---------------------------------------------------------------------------
var bookstoreConfig = new BookstoreConfiguration(builder.Configuration);
builder.Services.AddSingleton(bookstoreConfig);

// ---------------------------------------------------------------------------
// Logging — NLog with conditional AWS CloudWatch target
// (carries over LoggingSetup.ConfigureLogging)
// ---------------------------------------------------------------------------
var nlogConfig = new LoggingConfiguration();
NLog.Targets.Target loggingTarget;

if (bookstoreConfig.GetSetting("Services/LoggingService") == "aws")
{
    loggingTarget = new AWSTarget { LogGroup = "BobsBookstore" };
}
else
{
    loggingTarget = new DebuggerTarget();
}

nlogConfig.AddTarget("loggingTarget", loggingTarget);
nlogConfig.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Info, loggingTarget));
LogManager.Configuration = nlogConfig;

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// ---------------------------------------------------------------------------
// SSM parameter retrieval — ConfigurationSetup.ConfigureConfiguration
// Fetches settings from AWS Systems Manager when running against AWS services.
// ---------------------------------------------------------------------------
if (bookstoreConfig.GetSetting("Services/Database") == "aws")
{
    var rootPath = "/BobsBookstore";
    using var ssmClient = new AmazonSimpleSystemsManagementClient();
    var request = new GetParameterRequest
    {
        Name = $"{rootPath}/Database/ConnectionStrings/BookstoreDatabaseConnection"
    };
    var response = await ssmClient.GetParameterAsync(request);
    bookstoreConfig.AddConnectionString("BookstoreDatabaseConnection", response.Parameter.Value);
}

if (bookstoreConfig.GetSetting("Services/Authentication") == "aws")
{
    var rootPath = "/BobsBookstore";
    using var ssmClient = new AmazonSimpleSystemsManagementClient();
    var request = new GetParametersByPathRequest
    {
        Path = $"{rootPath}/Authentication/",
        Recursive = true
    };
    var response = await ssmClient.GetParametersByPathAsync(request);
    foreach (var parameter in response.Parameters)
    {
        bookstoreConfig.AddSetting(parameter.Name.Replace($"{rootPath}/", string.Empty), parameter.Value);
    }
}

if (bookstoreConfig.GetSetting("Services/FileService") == "aws")
{
    var rootPath = "/BobsBookstore";
    using var ssmClient = new AmazonSimpleSystemsManagementClient();
    var request = new GetParametersByPathRequest
    {
        Path = $"{rootPath}/Files/",
        Recursive = true
    };
    var response = await ssmClient.GetParametersByPathAsync(request);
    foreach (var parameter in response.Parameters)
    {
        bookstoreConfig.AddSetting(parameter.Name.Replace($"{rootPath}/", string.Empty), parameter.Value);
    }
}

// ---------------------------------------------------------------------------
// EF Core — ApplicationDbContext with SQL Server provider
// ---------------------------------------------------------------------------
var connectionString = bookstoreConfig.GetConnectionString("BookstoreDatabaseConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// ---------------------------------------------------------------------------
// Domain services (carries over DependencyInjectionSetup registrations)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IReferenceDataService, ReferenceDataService>();
builder.Services.AddScoped<IOfferService, OfferService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IShoppingCartService, ShoppingCartService>();
builder.Services.AddScoped<IImageResizeService, ImageResizeService>();

// ---------------------------------------------------------------------------
// Repositories
// ---------------------------------------------------------------------------
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IAddressRepository, AddressRepository>();
builder.Services.AddScoped<IBookRepository, BookRepository>();
builder.Services.AddScoped<IOfferRepository, OfferRepository>();
builder.Services.AddScoped<IShoppingCartRepository, ShoppingCartRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();
builder.Services.AddScoped(typeof(IPaginatedList<>), typeof(PaginatedList<>));

// ---------------------------------------------------------------------------
// File service — conditional AWS S3 vs local
// ---------------------------------------------------------------------------
if (bookstoreConfig.GetSetting("Services/FileService") == "aws")
{
    builder.Services.AddSingleton<IAmazonS3, AmazonS3Client>();
    builder.Services.AddScoped<IFileService, S3FileService>();
}
else
{
    var webRootPath = Path.Combine(builder.Environment.WebRootPath ?? builder.Environment.ContentRootPath, "Content");
    builder.Services.AddSingleton<IFileService>(new LocalFileService(webRootPath));
}

// ---------------------------------------------------------------------------
// Image validation service — conditional AWS Rekognition vs local
// ---------------------------------------------------------------------------
if (bookstoreConfig.GetSetting("Services/ImageValidationService") == "aws")
{
    builder.Services.AddSingleton<IAmazonRekognition, AmazonRekognitionClient>();
    builder.Services.AddScoped<IImageValidationService, RekognitionImageValidationService>();
}
else
{
    builder.Services.AddScoped<IImageValidationService, LocalImageValidationService>();
}

// ---------------------------------------------------------------------------
// Authentication — dual mode: local dev middleware + Cognito OpenIdConnect
// (carries over AuthenticationSetup.ConfigureAuthentication)
// ---------------------------------------------------------------------------
if (bookstoreConfig.GetSetting("Services/Authentication") == "aws")
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.MetadataAddress = bookstoreConfig.GetSetting("Authentication/Cognito/MetadataAddress");
        options.ClientId = bookstoreConfig.GetSetting("Authentication/Cognito/LocalClientId");
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Events = new OpenIdConnectEvents
        {
            OnTokenValidated = context =>
            {
                // Map Cognito claims to standard ClaimTypes if needed
                return Task.CompletedTask;
            }
        };
    });
}
else
{
    // Local authentication uses LocalAuthenticationMiddleware to set HttpContext.User.
    // A cookie scheme is still registered so the [Authorize] filter can challenge.
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie();
}

builder.Services.AddAuthorization();

// ---------------------------------------------------------------------------
// MVC — controllers with views + global filters
// (carries over FilterConfig: HandleErrorAttribute → exception handler,
//  AuthorizeAttribute → global authorize filter)
// ---------------------------------------------------------------------------
builder.Services.AddControllersWithViews(options =>
{
    // Global [Authorize] filter — all actions require authentication by default.
    // Individual controllers/actions can opt out with [AllowAnonymous].
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter());
});

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Middleware pipeline — order is load-bearing; do not reorder.
// ---------------------------------------------------------------------------

// Application_Error equivalent — centralised unhandled-exception logging.
// Placed early so it catches exceptions from all downstream middleware.
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (Exception ex)
    {
        var logger = LogManager.GetCurrentClassLogger();
        logger.Error(ex, "Unhandled exception");
        throw; // Re-throw so UseExceptionHandler can render the error page
    }
});

if (!app.Environment.IsDevelopment())
{
    // HandleErrorAttribute → UseExceptionHandler
    app.UseExceptionHandler("/Admin/Error/Index");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// [PORT-NOTE] Dropped BundleConfig.RegisterBundles — System.Web.Optimization
// bundling has no ASP.NET Core equivalent. Static assets are served directly
// from wwwroot/ via UseStaticFiles().

app.UseRouting();

// Local dev authentication middleware — runs before UseAuthentication
// so it can set HttpContext.User for non-AWS environments.
if (bookstoreConfig.GetSetting("Services/Authentication") != "aws")
{
    app.UseMiddleware<LocalAuthenticationMiddleware>();
}

app.UseAuthentication();
app.UseAuthorization();

// ---------------------------------------------------------------------------
// Routing — carries over RouteConfig.RegisterRoutes + Admin area
// ---------------------------------------------------------------------------

// Admin area route (carries over AdminAreaRegistration)
app.MapAreaControllerRoute(
    name: "Admin_default",
    areaName: "Admin",
    pattern: "Admin/{controller=Dashboard}/{action=Index}/{id?}");

// Default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
