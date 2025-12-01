using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using Groovo;
using Groovo.Data.Contexts;
using Groovo.DTOs;
using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;
using Groovo.Models;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Groovo.Tests.Factories;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string JwtSecret = "ThisIsATestSecretKeyForJWTTokenGenerationWithAtLeast32Characters";
    public const string JwtIssuer = "TestIssuer";
    public const string JwtAudience = "TestAudience";

    private readonly string _databaseName = $"TestDb_{Guid.NewGuid()}";
    private static readonly InMemoryDatabaseRoot _databaseRoot = new InMemoryDatabaseRoot();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
              ["JwtSettings:Secret"] = JwtSecret,
              ["JwtSettings:Issuer"] = JwtIssuer,
              ["JwtSettings:Audience"] = JwtAudience,
              ["JwtSettings:ExpiryMinutes"] = "60" 
            }!);
        });

        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registrations
            var descriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>))
                .ToList();
            foreach (var descriptor in descriptors)
                services.Remove(descriptor);

            // Add EF Core InMemory database (better for testing than SQLite)
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName, _databaseRoot);
                options.EnableSensitiveDataLogging();
            });

            // JWT config for tests
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = JwtIssuer,
                    ValidAudience = JwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)),
                    ClockSkew = TimeSpan.Zero
                };
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Seed database after host is built
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Ensure tables are created
            context.Database.EnsureCreated();

            // Seed data
            SeedTestData(context);
        }

        return host;
    }

    private void SeedTestData(ApplicationDbContext context)
    {
        
        // --- Users ---
        var adminUser = new User
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Admin User",
            Email = "admin@test.com",
            PasswordHash = "hashedpassword",
            Role = UserRole.Admin,
            Bio = "Admin bio",
            ImageUrl = "https://example.com/admin.png",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var regularUser = new User
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = "Regular User",
            Email = "user@test.com",
            PasswordHash = "hashedpassword",
            Role = UserRole.User,
            Bio = "Regular user bio",
            ImageUrl = "https://example.com/user.png",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var authorUser = new User
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Name = "Author User",
            Email = "author@test.com",
            PasswordHash = "hashedpassword",
            Role = UserRole.Author,
            Bio = "Author bio",
            ImageUrl = "https://example.com/author.png",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Users.AddRange(adminUser, regularUser, authorUser);

        // --- Playlists ---
        var publicPlaylist = new Playlist
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Name = "Public Playlist",
            Description = "A public playlist",
            IsPublic = true,
            IsAlbum = false,
            IsActive = true,
            TotalTime = 600,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var privatePlaylist = new Playlist
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Name = "Private Playlist",
            Description = "A private playlist",
            IsPublic = false,
            IsAlbum = false,
            IsActive = true,
            TotalTime = 300,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var album = new Playlist
        {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            Name = "Test Album",
            Description = "An album",
            IsPublic = true,
            IsAlbum = true,
            IsActive = true,
            TotalTime = 1200,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Playlists.AddRange(publicPlaylist, privatePlaylist, album);

        // --- Playlist Owners ---
        context.PlaylistOwners.AddRange(
            new PlaylistOwner { PlaylistId = publicPlaylist.Id, UserId = regularUser.Id },
            new PlaylistOwner { PlaylistId = privatePlaylist.Id, UserId = regularUser.Id },
            new PlaylistOwner { PlaylistId = album.Id, UserId = authorUser.Id }
        );

        // --- Songs ---
        var song1 = new Song
        {
            Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            Name = "Test Song 1",
            Album = album.Id,
            AudioUrl = "http://test.com/song1.mp3",
            ReleaseDate = DateTime.UtcNow,
            Length = 180,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var song2 = new Song
        {
            Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            Name = "Test Song 2",
            Album = album.Id,
            AudioUrl = "http://test.com/song2.mp3",
            ReleaseDate = DateTime.UtcNow,
            Length = 240,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Songs.AddRange(song1, song2);

        // --- PlaylistSongs ---
        context.PlaylistSongs.Add(new PlaylistSong
        {
            PlaylistId = publicPlaylist.Id,
            SongId = song1.Id,
            Order = 0
        });

        context.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        // InMemory database doesn't need cleanup
        base.Dispose(disposing);
    }
}