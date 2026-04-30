using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SalesCRM.Core.Interfaces;
using SalesCRM.Core.Options;
using SalesCRM.Infrastructure.Data;
using SalesCRM.Infrastructure.Repositories;
using SalesCRM.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Load config from solution root (parent of the project directory)
var solutionRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, ".."));
var configBasePath = File.Exists(Path.Combine(solutionRoot, "appsettings.json"))
    ? solutionRoot
    : builder.Environment.ContentRootPath;

builder.Configuration
    .SetBasePath(configBasePath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Resolve GCP credentials path to absolute
var gcpCredPath = builder.Configuration["Gcp:CredentialsPath"];
if (!string.IsNullOrWhiteSpace(gcpCredPath) && !Path.IsPathRooted(gcpCredPath))
{
    var resolved = Path.GetFullPath(Path.Combine(configBasePath, gcpCredPath));
    builder.Configuration["Gcp:CredentialsPath"] = resolved;
}

// Add EF Core with PostgreSQL (read from appsettings.json)
var dbHost = builder.Configuration["Database:DB_HOST"];
var dbDatabase = builder.Configuration["Database:DB_DATABASE"];
var dbUsername = builder.Configuration["Database:DB_USERNAME"];
var dbPassword = builder.Configuration["Database:DB_PASSWORD"];
var dbPort = builder.Configuration["Database:DB_PORT"];

var connectionString = $"Host={dbHost};Database={dbDatabase};Username={dbUsername};Password={dbPassword};Port={dbPort}";
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(
        connectionString,
        npgsqlOptions =>
        {
            npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
        });

    options.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);

    options.EnableDetailedErrors();
    options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
});

// Repository & UoW
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ILeadService, LeadService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddScoped<IDealService, DealService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ITargetService, TargetService>();
builder.Services.AddScoped<IGeofenceService, GeofenceService>();
builder.Services.AddScoped<ITrackingService, TrackingService>();
builder.Services.AddScoped<ITrackingHubNotifier, SalesCRM.API.Hubs.TrackingHubNotifier>();
builder.Services.AddScoped<ISchoolService, SchoolService>();
builder.Services.AddScoped<ISchoolAssignmentService, SchoolAssignmentService>();
builder.Services.AddScoped<IDemoService, DemoService>();
builder.Services.AddScoped<IDemoRecordingService, DemoRecordingService>();
builder.Services.AddScoped<IOnboardService, OnboardService>();
builder.Services.AddScoped<IVisitReportService, VisitReportService>();
builder.Services.AddScoped<IRoutePlanService, RoutePlanService>();
builder.Services.AddScoped<IAllowanceConfigService, AllowanceConfigService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ICalendarService, CalendarService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddSingleton<IGcpStorageService, GcpStorageService>();
builder.Services.AddHttpClient<IGeminiService, GeminiService>();
builder.Services.AddHttpClient<IGoogleRoadsService, GoogleRoadsService>();
builder.Services.AddSingleton<IPushNotificationService, PushNotificationService>();
builder.Services.AddScoped<IAiReportService, AiReportService>();
builder.Services.AddScoped<IDeviceFraudService, DeviceFraudService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IWeeklyPlanService, WeeklyPlanService>();
builder.Services.AddScoped<ISchoolProfileService, SchoolProfileService>();
builder.Services.AddScoped<ILeaveService, LeaveService>();
builder.Services.AddScoped<IExpenseClaimService, ExpenseClaimService>();

// AiReport scheduling options
builder.Services.Configure<AiReportOptions>(builder.Configuration.GetSection(AiReportOptions.SectionName));

// Background services
builder.Services.AddHostedService<SalesCRM.API.Services.FollowUpReminderService>();
builder.Services.AddHostedService<SalesCRM.API.Services.MidnightResetService>();
builder.Services.AddHostedService<SalesCRM.API.Services.AiReportGenerationService>();

// SignalR for live tracking
builder.Services.AddSignalR();

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
        // Support JWT token from query string for SignalR WebSocket connections
        options.Events = new()
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Controllers + Swagger
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SalesCRM API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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

// Seed database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.Migrate();
    await DbSeeder.SeedAsync(context);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(errApp =>
{
    errApp.Run(async ctx =>
    {
        var feature = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var ex = feature?.Error;
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json";
        var msg = app.Environment.IsDevelopment()
            ? ex?.ToString()
            : ex?.Message ?? "An unexpected error occurred.";
        app.Logger.LogError(ex, "Unhandled exception: {Message}", ex?.Message);
        await ctx.Response.WriteAsJsonAsync(new { success = false, message = msg });
    });
});

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<SalesCRM.API.Hubs.TrackingHub>("/hubs/tracking");

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

var urls = builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5000";
app.Logger.LogInformation("Application running on: {Urls}", urls);

app.Run();
