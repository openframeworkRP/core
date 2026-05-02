using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OpenFramework.Api.Data;
using OpenFramework.Api.Services;

// Npgsql v6+ exige DateTimeKind.Utc par défaut. Ce switch réactive l'ancien
// comportement "permissif" qui accepte Kind=Unspecified (dates de naissance, etc.).
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// ── Tolérance de boot : l'API démarre même si la config est incomplète (wizard) ──
var jwtKey = builder.Configuration["Jwt:Key"] ?? "";
var connStr = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
var isConfigured = jwtKey.Length >= 32 && !string.IsNullOrWhiteSpace(connStr);
var effectiveJwtKey = isConfigured ? jwtKey : new string('0', 32);
var effectiveConnStr = string.IsNullOrWhiteSpace(connStr)
    ? "Host=localhost;Database=tempdb;Username=postgres;Password=placeholder;Connect Timeout=2;"
    : connStr;

if (!isConfigured)
{
    Console.WriteLine("[OpenFramework.Api] WARNING : configuration incomplète (Jwt:Key et/ou connection string).");
    Console.WriteLine("[OpenFramework.Api] Démarrage en mode 'waiting for configuration' — wizard web : http://localhost:4173/setup");
}

builder.Services.AddDbContext<OpenFrameworkDbContext>(options =>
    options.UseNpgsql(
        effectiveConnStr,
        pg => pg.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null)));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CharacterService>();
builder.Services.AddScoped<BankService>();
builder.Services.AddScoped<AtmService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<CacheService>();

// ── Cache distribué : Redis si configuré, sinon mémoire (fallback wizard) ────
var redisConn = builder.Configuration["Redis:ConnectionString"] ?? "";
if (!string.IsNullOrWhiteSpace(redisConn))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConn;
        options.InstanceName = "of:";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

// ── Compression des réponses HTTP (gzip / brotli) ─────────────────────────────
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "OpenFrameworkApi",
            ValidAudience = "OpenFrameworkPlayers",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(effectiveJwtKey))
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.WebHost.UseUrls("http://+:8443");
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OpenFramework API",
        Version = "v1",
        Description = "SMALL LIFE API"
    });
});

var app = builder.Build();

// ── Init DB : migrations automatiques (tolérant si DB injoignable au boot) ───
if (isConfigured)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpenFrameworkDbContext>();
        if (db.Database.GetPendingMigrations().Any())
            db.Database.Migrate();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[OpenFramework.Api] WARNING : init DB échouée ({ex.Message}). Les endpoints DB renverront 503 via /health/ready.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseResponseCompression();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ── Health ─────────────────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "healthy", configured = isConfigured }));

app.MapGet("/health/ready", async (OpenFrameworkDbContext db) =>
{
    if (!isConfigured)
        return Results.Json(new { ready = false, reason = "not-configured" }, statusCode: 503);
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        return canConnect
            ? Results.Ok(new { ready = true })
            : Results.Json(new { ready = false, reason = "db-unreachable" }, statusCode: 503);
    }
    catch (Exception ex)
    {
        return Results.Json(new { ready = false, reason = "db-error", error = ex.Message }, statusCode: 503);
    }
});

app.Run();
