using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OpenFramework.Api.Data;
using OpenFramework.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<OpenFrameworkDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CharacterService>();
builder.Services.AddScoped<BankService>();   
builder.Services.AddScoped<AtmService>();
builder.Services.AddScoped<InventoryService>();

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OpenFrameworkDbContext>();
    if (db.Database.GetPendingMigrations().Any())
    {
        db.Database.Migrate();
    }

    // ── Audit log tables (Sessions / ChatLogs / AdminActionLogs) ─────────────
    // Créées en SQL brut idempotent pour ne pas alourdir le snapshot EF.
    // À synchroniser dans une vraie migration EF lors du prochain dotnet ef
    // migrations add (supprimer alors les CREATE TABLE générés en double).
    db.Database.ExecuteSqlRaw(@"
        IF OBJECT_ID(N'[Sessions]', N'U') IS NULL
        BEGIN
            CREATE TABLE [Sessions] (
                [Id]              UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Sessions] PRIMARY KEY,
                [SteamId]         NVARCHAR(64)     NOT NULL DEFAULT '',
                [DisplayName]     NVARCHAR(256)    NOT NULL DEFAULT '',
                [JoinedAt]        DATETIME2        NOT NULL,
                [LeftAt]          DATETIME2        NULL,
                [DurationSeconds] INT              NULL
            );
            CREATE INDEX [IX_Sessions_SteamId]  ON [Sessions]([SteamId]);
            CREATE INDEX [IX_Sessions_JoinedAt] ON [Sessions]([JoinedAt]);
            CREATE INDEX [IX_Sessions_LeftAt]   ON [Sessions]([LeftAt]);
        END;

        IF OBJECT_ID(N'[ChatLogs]', N'U') IS NULL
        BEGIN
            CREATE TABLE [ChatLogs] (
                [Id]         UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_ChatLogs] PRIMARY KEY,
                [SentAt]     DATETIME2        NOT NULL,
                [SteamId]    NVARCHAR(64)     NOT NULL DEFAULT '',
                [AuthorName] NVARCHAR(256)    NOT NULL DEFAULT '',
                [Channel]    NVARCHAR(64)     NOT NULL DEFAULT '',
                [Message]    NVARCHAR(MAX)    NOT NULL DEFAULT '',
                [IsCommand]  BIT              NOT NULL DEFAULT 0
            );
            CREATE INDEX [IX_ChatLogs_SentAt]  ON [ChatLogs]([SentAt]);
            CREATE INDEX [IX_ChatLogs_SteamId] ON [ChatLogs]([SteamId]);
        END;

        IF OBJECT_ID(N'[AdminActionLogs]', N'U') IS NULL
        BEGIN
            CREATE TABLE [AdminActionLogs] (
                [Id]            UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_AdminActionLogs] PRIMARY KEY,
                [At]            DATETIME2        NOT NULL,
                [AdminSteamId]  NVARCHAR(64)     NOT NULL DEFAULT '',
                [Action]        NVARCHAR(64)     NOT NULL DEFAULT '',
                [TargetSteamId] NVARCHAR(64)     NULL,
                [Reason]        NVARCHAR(MAX)    NULL,
                [PayloadJson]   NVARCHAR(MAX)    NULL,
                [Source]        NVARCHAR(32)     NOT NULL DEFAULT 'web'
            );
            CREATE INDEX [IX_AdminActionLogs_At]            ON [AdminActionLogs]([At]);
            CREATE INDEX [IX_AdminActionLogs_AdminSteamId]  ON [AdminActionLogs]([AdminSteamId]);
            CREATE INDEX [IX_AdminActionLogs_TargetSteamId] ON [AdminActionLogs]([TargetSteamId]);
            CREATE INDEX [IX_AdminActionLogs_Action]        ON [AdminActionLogs]([Action]);
        END;

        IF OBJECT_ID(N'[PendingAdminCommands]', N'U') IS NULL
        BEGIN
            CREATE TABLE [PendingAdminCommands] (
                [Id]                      UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_PendingAdminCommands] PRIMARY KEY,
                [CreatedAt]               DATETIME2        NOT NULL,
                [RequestedByAdminSteamId] NVARCHAR(64)     NOT NULL DEFAULT '',
                [Command]                 NVARCHAR(64)     NOT NULL DEFAULT '',
                [TargetSteamId]           NVARCHAR(64)     NULL,
                [ArgsJson]                NVARCHAR(MAX)    NULL,
                [Status]                  NVARCHAR(16)     NOT NULL DEFAULT 'pending',
                [ProcessedAt]             DATETIME2        NULL,
                [Result]                  NVARCHAR(MAX)    NULL
            );
            CREATE INDEX [IX_PendingAdminCommands_Status_CreatedAt]
                ON [PendingAdminCommands]([Status], [CreatedAt]);
        END;

        IF OBJECT_ID(N'[CriminalRecords]', N'U') IS NULL
        BEGIN
            CREATE TABLE [CriminalRecords] (
                [Id]                NVARCHAR(64)  NOT NULL CONSTRAINT [PK_CriminalRecords] PRIMARY KEY,
                [Title]             NVARCHAR(256) NOT NULL DEFAULT '',
                [Description]       NVARCHAR(MAX) NOT NULL DEFAULT '',
                [FromWhoMandatedId] NVARCHAR(64)  NOT NULL DEFAULT '',
                [ToWhoCharacterId]  NVARCHAR(64)  NOT NULL DEFAULT ''
            );
            CREATE INDEX [IX_CriminalRecords_ToWhoCharacterId] ON [CriminalRecords]([ToWhoCharacterId]);
        END;

        IF OBJECT_ID(N'[InventoryLogs]', N'U') IS NULL
        BEGIN
            CREATE TABLE [InventoryLogs] (
                [Id]           UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_InventoryLogs] PRIMARY KEY,
                [At]           DATETIME2        NOT NULL,
                [ActorSteamId] NVARCHAR(64)     NOT NULL DEFAULT '',
                [CharacterId]  NVARCHAR(64)     NULL,
                [Action]       NVARCHAR(32)     NOT NULL DEFAULT '',
                [ItemGameId]   NVARCHAR(128)    NOT NULL DEFAULT '',
                [Count]        INT              NOT NULL DEFAULT 0,
                [SourceType]   NVARCHAR(32)     NOT NULL DEFAULT '',
                [SourceId]     NVARCHAR(128)    NULL,
                [TargetType]   NVARCHAR(32)     NOT NULL DEFAULT '',
                [TargetId]     NVARCHAR(128)    NULL,
                [MetadataJson] NVARCHAR(MAX)    NULL
            );
            CREATE INDEX [IX_InventoryLogs_At]              ON [InventoryLogs]([At]);
            CREATE INDEX [IX_InventoryLogs_ActorSteamId]    ON [InventoryLogs]([ActorSteamId]);
            CREATE INDEX [IX_InventoryLogs_ItemGameId]      ON [InventoryLogs]([ItemGameId]);
            CREATE INDEX [IX_InventoryLogs_Action]          ON [InventoryLogs]([Action]);
            CREATE INDEX [IX_InventoryLogs_ItemGameId_At]   ON [InventoryLogs]([ItemGameId], [At]);
        END;
    ");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();


app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();