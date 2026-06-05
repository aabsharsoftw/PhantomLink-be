using Hangfire;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using PhantomPulse.Api.Middleware;
using PhantomPulse.Automation;
using PhantomPulse.Campaigns;
using PhantomPulse.Crm;
using PhantomPulse.Foundation;
using PhantomPulse.Infrastructure;
using PhantomPulse.Infrastructure.Persistence.Seeding;
using PhantomPulse.Infrastructure.Realtime;
using PhantomPulse.Messaging;
using PhantomPulse.SharedKernel.Domain;
using Serilog;
using System.Text;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.MapInboundClaims = false; // keep "sub", "email" etc. as-is; don't rename to ClaimTypes.*
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Auth:Secret"]!));
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Auth:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Auth:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization();

// Phase 0
builder.Services.AddFoundationModule();

// Phase 1
builder.Services.AddCrmModule();
builder.Services.AddMessagingModule(builder.Configuration);
builder.Services.AddAutomationModule();
builder.Services.AddCampaignsModule(builder.Configuration);

// Phase 2 (uncomment when ready)
// builder.Services.AddSocialModule();
// builder.Services.AddPaymentsModule(builder.Configuration);

// Phase 3 (uncomment when ready)
// builder.Services.AddAdminModule();
// builder.Services.AddWhiteLabelModule();
// builder.Services.AddTelephonyModule();

builder.Services.AddCors(opt => opt.AddDefaultPolicy(p => p
    .WithOrigins(
        "http://localhost:5000",
        "http://localhost:5173",
        "http://localhost:5174",
        "http://localhost:5175",
        "http://localhost:5176",
        "http://localhost:5177",
        "http://localhost:5157",
        "http://localhost:5158",
        "http://localhost:3000",
        "http://localhost:4173")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddControllers(opt => opt.Conventions.Add(new ApiPrefixConvention()))
    .AddJsonOptions(opt =>
        opt.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddOpenApi(opt =>
{
    opt.AddDocumentTransformer((doc, ctx, ct) =>
    {
        doc.Info = new OpenApiInfo { Title = "PhantomPulse API", Version = "v1" };
        return Task.CompletedTask;
    });
});

var app = builder.Build();

await DatabaseSeeder.RunAsync(app.Services);

app.UseSerilogRequestLogging();
app.UseCors();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.MapOpenApi();
app.UseSwaggerUI(opt =>
{
    opt.SwaggerEndpoint("/openapi/v1.json", "PhantomPulse API v1");
    opt.RoutePrefix = "swagger";
});
app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseMiddleware<PermissionEnforcementMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapHangfireDashboard("/jobs");
app.MapHub<InboxHub>("/hubs/inbox");

app.Run();

// Prepends "api" to every attribute-routed controller so all endpoints live under /api/*
internal sealed class ApiPrefixConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
            foreach (var selector in controller.Selectors.Where(s => s.AttributeRouteModel is not null))
                selector.AttributeRouteModel = AttributeRouteModel.CombineAttributeRouteModel(
                    new AttributeRouteModel { Template = "api" },
                    selector.AttributeRouteModel);
    }
}
