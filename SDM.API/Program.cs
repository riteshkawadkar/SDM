using Microsoft.EntityFrameworkCore;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using SDM.Application.Interfaces;
using SDM.Application.Settings;
using SDM.Infrastructure.Data;
using SDM.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Swagger/OpenAPI for .NET 8
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

// Device JWT generator already registered via IJwtTokenGenerator

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Application services
builder.Services.AddHttpClient();
builder.Services.AddScoped<IDeviceService, SDM.Infrastructure.Services.DeviceService>();
builder.Services.AddScoped<ICommandService, SDM.Infrastructure.Services.CommandService>();
builder.Services.AddScoped<IPushService, SDM.Infrastructure.Services.PushService>();

// JWT Authentication
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"];
if (!string.IsNullOrEmpty(jwtKey))
{
    var key = Encoding.UTF8.GetBytes(jwtKey);
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });
}

// Hangfire (optional via configuration: Hangfire:Enabled = true/false)
var hangfireEnabled = builder.Configuration.GetValue<bool?>("Hangfire:Enabled") ?? true;
if (hangfireEnabled)
{
    builder.Services.AddHangfire(config =>
    {
        config.UsePostgreSqlStorage(builder.Configuration.GetConnectionString("DefaultConnection"));
    });

    builder.Services.AddHangfireServer();

    // Register Hangfire job class
    builder.Services.AddScoped<SDM.Infrastructure.Services.HangfireJobs>();
}

var app = builder.Build();

// Apply EF Core migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Hangfire dashboard (development only) - only when Hangfire is enabled
if (app.Environment.IsDevelopment() && (hangfireEnabled))
{
    app.UseHangfireDashboard("/hangfire");
    // schedule recurring job to process pending commands every minute
    RecurringJob.AddOrUpdate<SDM.Infrastructure.Services.HangfireJobs>(
        "process-pending-commands",
        job => job.ProcessPendingCommands(),
        Cron.Minutely);
}

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

app.UseHttpsRedirection();

app.MapControllers();

app.Run();

// Expose Program class for WebApplicationFactory in tests
public partial class Program { }
