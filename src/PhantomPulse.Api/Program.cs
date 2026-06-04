using PhantomPulse.Infrastructure;
using PhantomPulse.Infrastructure.Persistence;
using PhantomPulse.Foundation;
using PhantomPulse.Foundation.Authorization;
using PhantomPulse.Crm;
using PhantomPulse.Messaging;
using PhantomPulse.Automation;
using PhantomPulse.Campaigns;
using PhantomPulse.SharedKernel.Domain;
using PhantomPulse.Infrastructure.Realtime;
using Serilog;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Hangfire;
using Microsoft.OpenApi.Models;
using PhantomPulse.Api.Middleware;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Auth:Secret"]!));
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey        = key,
            ValidateIssuer          = true,
            ValidIssuer             = builder.Configuration["Auth:Issuer"],
            ValidateAudience        = true,
            ValidAudience           = builder.Configuration["Auth:Audience"],
            ValidateLifetime        = true,
            ClockSkew               = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization(options =>
{
    foreach (var (key, _, _, _) in PermissionKeys.All)
        options.AddPolicy(key, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireClaim("permissions", key));

    options.AddPolicy("SuperAdmin", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("role", "SuperAdmin"));
});

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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "PhantomPulse API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name        = "Authorization",
        Type        = SecuritySchemeType.Http,
        Scheme      = "bearer",
        BearerFormat = "JWT",
        In          = ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your JWT token.",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

await DataSeeder.RunAsync(app.Services);

app.UseSerilogRequestLogging();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "PhantomPulse API v1"));
app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseMiddleware<PermissionEnforcementMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapHangfireDashboard("/jobs");
app.MapHub<InboxHub>("/hubs/inbox");

app.Run();
