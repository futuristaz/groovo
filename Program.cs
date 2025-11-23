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

namespace Groovo;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add Entity Framework
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            //options.UseInMemoryDatabase("GroovoInMemoryDb");
            options.UseSqlite("Data Source=development.sqlite");

            // Enable detailed error messages in development
            if (builder.Environment.IsDevelopment())
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        // Register services
        builder.Services.AddScoped<IJwtService, JwtService>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IPasswordHasher<Models.User>, PasswordHasher<Models.User>>();
        
        // Register TUS services
        builder.Services.AddSingleton<TusStorageConfiguration>();
        builder.Services.AddScoped<ISongFileService, SongFileService>();
        builder.Services.AddSingleton<TusConfigurationFactory>();
    
        // TUS cleanup background service
        builder.Services.AddHostedService<TusCleanupService>();

        builder.Services.AddJwtAuthentication(builder.Configuration);
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminPolicy", policy => policy.RequireRole("Admin"));
            options.AddPolicy("AuthorPolicy", policy => policy.RequireRole("Author"));
            options.AddPolicy("UserPolicy", policy => policy.RequireRole("User"));
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

        var app = builder.Build();

        // Ensure database is created and seed development data
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // For development, recreate database with fresh seed data
            if (app.Environment.IsDevelopment())
            {
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();
                await DatabaseSeeder.SeedAsync(context);
            }
            else
            {
                // For production, just ensure database exists
                await context.Database.EnsureCreatedAsync();
            }
        }
        
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseTus(context => context.RequestServices
            .GetRequiredService<TusConfigurationFactory>()
            .GetConfiguration());

        // Use official Authentication and Authorization middleware
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        app.MapHub<PlaylistHub>("/live");
        
        app.Run();
    }
}