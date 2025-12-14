using Groovo.Filters;
using Groovo.Data.Contexts;
using Groovo.Data;
using Groovo.Hubs;
using Groovo.Services;
using Groovo.Services.Tus;
using Groovo.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Reflection;
using tusdotnet;
using Serilog;
using Groovo.Services.Hub;
using System.Diagnostics.CodeAnalysis;

namespace Groovo;

[ExcludeFromCodeCoverage]
public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var DefaultCorsPolicy = "_AllowAllPolicy";

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Debug();
  
        loggerConfig.WriteTo.File("logs/app.log",
            rollingInterval: RollingInterval.Day,
            restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning
        );

        if (builder.Environment.IsDevelopment())
        {
            loggerConfig.WriteTo.Console();
        }

        Log.Logger = loggerConfig.CreateLogger();
        builder.Host.UseSerilog();

        // Only configure database for non-testing environments
        if (!builder.Environment.IsEnvironment("Testing"))
        {
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
            {
                //options.UseInMemoryDatabase("GroovoInMemoryDb");
                var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
                options.UseSqlite(connectionString);

                if (builder.Environment.IsDevelopment())
                {
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                }
            });
        }

        // Register repositories
        builder.Services.AddScoped<Repositories.IPlaylistRepository, Repositories.PlaylistRepository>();
        builder.Services.AddScoped<Repositories.ISongRepository, Repositories.SongRepository>();
        builder.Services.AddScoped<Repositories.IUserRepository, Repositories.UserRepository>();
        builder.Services.AddScoped<Repositories.IRefreshTokenRepository, Repositories.RefreshTokenRepository>();

        // Register services
        builder.Services.AddScoped<IJwtService, JwtService>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IAuthorService, AuthorService>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<ISongService, SongService>();
        builder.Services.AddScoped<IPlaylistService, PlaylistService>();
        builder.Services.AddScoped<IPasswordHasher<Models.User>, PasswordHasher<Models.User>>();
        
        // Register TUS services
        builder.Services.AddSingleton<TusStorageConfiguration>();
        builder.Services.AddScoped<ISongFileService, SongFileService>();
        builder.Services.AddSingleton<TusConfigurationFactory>();
        builder.Services.AddHostedService<TusCleanupService>();

        builder.Services.AddJwtAuthentication(builder.Configuration);
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminPolicy", policy => policy.RequireRole("Admin"));
            options.AddPolicy("AuthorPolicy", policy => policy.RequireRole("Author"));
            options.AddPolicy("UserPolicy", policy => policy.RequireRole("User"));
        });

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(name: DefaultCorsPolicy,
                policy =>
                {
                    var allowedOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>();
                    
                    if (allowedOrigins != null && allowedOrigins.Length > 0)
                    {
                        policy.WithOrigins(allowedOrigins)
                            .AllowAnyMethod()
                            .AllowAnyHeader()
                            .AllowCredentials()
                            .WithExposedHeaders("Tus-Resumable", "Upload-Offset", "Upload-Length", "Location", "Upload-Metadata");
                    }
                });
        });

        builder.Services.AddControllers(options =>
        {
            options.Filters.Add<ApiResponseFilter>();
        });

        builder.Services.AddApiVersioning(options =>
        {
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
            options.ReportApiVersions = true;
        });
        
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "Groovo API", Version = "v1" });
            c.SchemaFilter<EnumDescriptionSchemaFilter>();
            
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        });
        builder.Services.AddVersionedApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        builder.Services.AddSignalR();
        builder.Services.AddSingleton<IUserPlaylistTracker<string, string>, UserPlaylistTracker>();
        builder.Services.AddSingleton<IPlaybackStateStore<string, DTOs.PlaybackState>, PlaybackStateStore>();
        builder.Services.AddHostedService<PlaybackUpdateService>();

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // For development, recreate database with fresh seed data only if --reset-db flag is passed
            if (app.Environment.IsDevelopment())
            {
                var resetDb = args.Contains("--reset-db", StringComparer.OrdinalIgnoreCase);
                var enableSeeding = args.Contains("--seed-db", StringComparer.OrdinalIgnoreCase);

                if (resetDb)
                {
                    await context.Database.EnsureDeletedAsync();
                    await context.Database.EnsureCreatedAsync();
                    if (enableSeeding)
                    {
                        await DatabaseSeeder.SeedAsync(context);
                    }
                }
                else
                {
                    await context.Database.EnsureCreatedAsync();
                }
            }
            else
            {
                await context.Database.EnsureCreatedAsync();
            }
        }
        
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseCors(DefaultCorsPolicy);
        }

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseTus(context => context.RequestServices
            .GetRequiredService<TusConfigurationFactory>()
            .GetConfiguration());

        var storageConfig = app.Services.GetRequiredService<TusStorageConfiguration>();
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
                Path.Combine(Directory.GetCurrentDirectory(), storageConfig.FinalPath)),
            RequestPath = "/contents",
            ServeUnknownFileTypes = true,
            DefaultContentType = "application/octet-stream"
        });

        app.MapControllers();

        app.MapHub<PlaylistHub>("/live");
        
        app.Run();
    }
}