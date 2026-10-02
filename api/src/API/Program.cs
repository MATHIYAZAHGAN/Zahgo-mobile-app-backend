using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;

// Render injects a PORT env var at runtime. Override Kestrel's URL so the
// container actually listens on that port regardless of appsettings.json.
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
Environment.SetEnvironmentVariable("ASPNETCORE_URLS", $"http://+:{port}");

var builder = WebApplication.CreateBuilder(args);

// Load .env file
var envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envFilePath))
{
    foreach (var line in File.ReadAllLines(envFilePath))
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
            continue;

        var parts = line.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
        }
    }
}

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/zahsellerai-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Configure JSON serialization to be more flexible
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ZAH Seller AI API",
        Version = "v1",
        Description = "AI-Powered Reseller Catalog Platform API",
        Contact = new OpenApiContact
        {
            Name = "ZAH Seller AI",
            Email = "support@zahsellerai.com"
        }
    });

    // Add JWT authentication to Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure JWT Authentication
var jwtSecret = builder.Configuration["JWT_SECRET"] 
    ?? builder.Configuration["JWT:Secret"]
    ?? Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? "zahgo*51228-ZahSellerAI-JWT-Secret-Key-2024";
var jwtIssuer = builder.Configuration["JWT_ISSUER"] 
    ?? builder.Configuration["JWT:Issuer"]
    ?? Environment.GetEnvironmentVariable("JWT_ISSUER")
    ?? "ZahSellerAI";
var jwtAudience = builder.Configuration["JWT_AUDIENCE"] 
    ?? builder.Configuration["JWT:Audience"]
    ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE")
    ?? "ZahSellerAI-Mobile";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Configure CORS
var corsOrigins = builder.Configuration["CORS_ORIGINS"]?.Split(',') ?? new[] { "*" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowMobileApp", policy =>
    {
        // Allow all origins in development
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Configure MongoDB
builder.Services.Configure<ZahSellerAI.Infrastructure.MongoDB.MongoDbSettings>(options =>
{
    options.ConnectionString = builder.Configuration["MONGODB_CONNECTION_STRING"] 
        ?? builder.Configuration["MongoDB:ConnectionString"]
        ?? "mongodb://localhost:27017";
    options.DatabaseName = builder.Configuration["MONGODB_DATABASE_NAME"] 
        ?? builder.Configuration["MongoDB:DatabaseName"]
        ?? "zahgo";
});

builder.Services.AddSingleton<ZahSellerAI.Infrastructure.MongoDB.MongoDbContext>();

// Add Application Services
builder.Services.AddScoped<ZahSellerAI.Application.Interfaces.IAuthenticationService, 
    ZahSellerAI.Infrastructure.Services.AuthenticationService>();

// Configure SignalR
builder.Services.AddSignalR();

// Add AI Infrastructure Services
builder.Services.AddSingleton<ZahSellerAI.Application.Interfaces.IFileStorageService,
    ZahSellerAI.Infrastructure.Storage.LocalFileStorageService>();

// Provider-independent image storage (local in development, object storage in production)
builder.Services.AddScoped<ZahSellerAI.Application.Interfaces.IImageStorageService,
    ZahSellerAI.Infrastructure.Storage.ImageStorageService>();

// Direct Gemini image generation / editing service
builder.Services.AddHttpClient<ZahSellerAI.Application.Interfaces.IGeminiImageService,
    ZahSellerAI.Infrastructure.AI.GeminiImageService>();

// Add HttpClient factory for Remove.bg and other HTTP services
builder.Services.AddHttpClient();

// Legacy background-removal pipeline (kept for backward compatibility, no longer used by AI Product Studio)
builder.Services.AddScoped<ZahSellerAI.Application.Interfaces.IBackgroundRemovalProvider,
    ZahSellerAI.Infrastructure.AI.RembgBackgroundRemovalProvider>();
builder.Services.AddScoped<ZahSellerAI.Application.Interfaces.IImageGenerationProvider,
    ZahSellerAI.Infrastructure.AI.AmazonStudioImageGenerationProvider>();
builder.Services.AddScoped<ZahSellerAI.Application.Interfaces.IImageAnalysisProvider,
    ZahSellerAI.Infrastructure.AI.OpenAiVisionAnalysisProvider>();

// Add Infrastructure Services
builder.Services.AddScoped<ZahSellerAI.Infrastructure.MongoDB.Repositories.IProductRepository,
    ZahSellerAI.Infrastructure.MongoDB.Repositories.ProductRepository>();

// Add Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Enable Swagger in all environments so the deployed API is easy to verify
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "ZAH Seller AI API v1");
    options.RoutePrefix = string.Empty; // Serve Swagger UI at root
});

if (app.Environment.IsDevelopment())
{
    // Development-only middleware can go here
}

app.UseSerilogRequestLogging();

// HTTPS redirect is intentionally omitted: Render terminates TLS at the edge
// and forwards plain HTTP internally. Redirecting here would cause a redirect loop.

app.UseStaticFiles();

app.UseCors("AllowMobileApp");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ZahSellerAI.API.Hubs.AIProcessingHub>("/hubs/ai-processing");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    version = "1.0.0"
}));

try
{
    Log.Information("Starting ZAH Seller AI API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start");
}
finally
{
    Log.CloseAndFlush();
}
