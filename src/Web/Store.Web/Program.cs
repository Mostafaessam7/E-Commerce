using Identity.Infrastructure;
using Infrastructure;
using Microsoft.AspNetCore.Builder;
using Observability;
using Security;
using Store.Web.Infrastructure.ExceptionHandling;

var builder = WebApplication.CreateBuilder(args);

// --- Cross-cutting building blocks (Phase 1 foundation) ---
builder.Services.AddObservabilityCore();
builder.Services.AddSecurityCore();
builder.Services.AddSharedInfrastructure();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// --- Module composition root ---
// Every module owns its own `Add{Module}Module(IServiceCollection, IConfiguration)` extension
// in that module's Infrastructure project (convention, not an interface — see
// docs/module-composition.md for why this beats a reflection-discovered IModule abstraction).
// Calls are added here as each module gets real services, starting Phase 4 (Catalog):
//
// builder.Services.AddCatalogModule(builder.Configuration);
// builder.Services.AddInventoryModule(builder.Configuration);
// builder.Services.AddOrderingModule(builder.Configuration);
// builder.Services.AddPaymentsModule(builder.Configuration);
// builder.Services.AddCustomersModule(builder.Configuration);
builder.Services.AddIdentityModule(builder.Configuration);
// builder.Services.AddPromotionsModule(builder.Configuration);
// builder.Services.AddShippingModule(builder.Configuration);
// builder.Services.AddReviewsModule(builder.Configuration);
// builder.Services.AddNotificationsModule(builder.Configuration);

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    // GlobalExceptionHandler handles JSON/API requests itself; anything it defers to (normal
    // page navigations) falls through to this Razor error view.
    ExceptionHandlingPath = "/Home/Error",
});

if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
