using Ryla.Api.Endpoints;
using Ryla.Api.Extensions;
using Ryla.Infrastructure;

var builder = WebApplication.CreateSlimBuilder(args);
// CreateSlimBuilder: AOT-optimized entry point
// ปิด reflection-based features (MVC conventions, XML serialization discovery)

builder.Services.ConfigureHttpJsonOptions(options =>
{
    // AOT-safe JSON: ทุก type ที่ serialize ต้องลงทะเบียนใน RylaJsonContext
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, RylaJsonContext.Default);
});

// OpenAPI — built-in .NET 10 (AOT-compatible, ไม่ใช้ Swashbuckle)
builder.Services.AddOpenApi();

builder.Services.AddRylaCoreServices(builder.Configuration);
builder.Services.AddRylaInfrastructureServices(builder.Configuration);

var app = builder.Build();

// OpenAPI endpoint: เปิดทั้ง dev และ prod เพื่อให้ feature-agent export spec ได้
// ใน production จะอยู่หลัง auth middleware
app.MapOpenApi();

// Auth middleware ต้องอยู่ก่อน endpoint routing
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapTikTokWebhookEndpoints();
app.MapShopeeWebhookEndpoints();
app.MapTestNotificationEndpoints();
app.MapSimulateOrderEndpoints();
app.MapProfitEndpoints();
app.MapShopeeOAuthEndpoints();

app.Run();
