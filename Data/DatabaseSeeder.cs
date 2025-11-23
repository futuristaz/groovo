using Groovo.Data.Contexts;
using Groovo.Models;
using Microsoft.EntityFrameworkCore;

namespace Groovo.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // Check if data already exists
        if (await context.Users.AnyAsync()) return;

        var now = DateTime.UtcNow;

        // Add sample users/authors
        var users = new[]
        {
            new User
            {
                Id = new Guid("11111111-1111-1111-1111-111111111111"),
                Name = "John Lennon",
                Bio = "English singer and songwriter",
                Email = "john.lennon@beatles.com",
                PasswordHash = "demo-password-hash-not-for-production",
                Role = UserRole.Author,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = new Guid("22222222-2222-2222-2222-222222222222"),
                Name = "Paul McCartney",
                Bio = "English singer and songwriter",
                Email = "paul.mccartney@beatles.com",
                PasswordHash = "demo-password-hash-not-for-production",
                Role = UserRole.Author,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = new Guid("33333333-3333-3333-3333-333333333333"),
                Name = "Freddie Mercury",
                Bio = "British singer and songwriter",
                Email = "freddie.mercury@queen.com",
                PasswordHash = "demo-password-hash-not-for-production",
                Role = UserRole.Author,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = new Guid("44444444-4444-4444-4444-444444444444"),
                Name = "David Bowie",
                Bio = "English singer and songwriter",
                Email = "david.bowie@starman.com",
                PasswordHash = "demo-password-hash-not-for-production",
                Role = UserRole.Author,
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        await context.Users.AddRangeAsync(users);
        await context.SaveChangesAsync();

        // Add sample songs
        var songs = new[]
        {
            new Song
            {
                Id = new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Name = "Imagine",
                Album = new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                AudioUrl = "https://example.com/imagine.mp3",
                Description = "A song about peace and unity",
                ReleaseDate = new DateTime(1971, 9, 9),
                Genre = "Rock",
                Tags = "peace,classic,rock",
                Length = 183,
                Plays = 1000000,
                Likes = 50000,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Song
            {
                Id = new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Name = "Yesterday",
                Album = new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                AudioUrl = "https://example.com/yesterday.mp3",
                Description = "A melancholic ballad about lost love",
                ReleaseDate = new DateTime(1965, 8, 6),
                Genre = "Pop",
                Tags = "classic,pop,ballad",
                Length = 125,
                Plays = 800000,
                Likes = 40000,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Song
            {
                Id = new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                Name = "Bohemian Rhapsody",
                Album = new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"),
                AudioUrl = "https://example.com/bohemian-rhapsody.mp3",
                Description = "An epic rock opera masterpiece",
                ReleaseDate = new DateTime(1975, 10, 31),
                Genre = "Rock",
                Tags = "opera,rock,classic",
                Length = 355,
                Plays = 1500000,
                Likes = 75000,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        await context.Songs.AddRangeAsync(songs);
        await context.SaveChangesAsync();

        // Add song-author relationships
        var songAuthors = new[]
        {
            new SongAuthor
            {
                SongId = new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), // Imagine
                UserId = new Guid("11111111-1111-1111-1111-111111111111")  // John Lennon
            },
            new SongAuthor
            {
                SongId = new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), // Yesterday
                UserId = new Guid("22222222-2222-2222-2222-222222222222")  // Paul McCartney
            },
            new SongAuthor
            {
                SongId = new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), // Bohemian Rhapsody
                UserId = new Guid("33333333-3333-3333-3333-333333333333")  // Freddie Mercury
            }
        };

        await context.SongAuthors.AddRangeAsync(songAuthors);
        await context.SaveChangesAsync();

        // Add sample playlists
        var playlists = new[]
        {
            new Playlist
            {
                Id = new Guid("99999999-9999-9999-9999-999999999999"),
                Name = "Classic Rock Hits",
                Description = "A collection of timeless rock classics",
                IsPublic = true,
                IsActive = false,
                IsAlbum = false,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Playlist
            {
                Id = new Guid("88888888-8888-8888-8888-888888888888"),
                Name = "Pop Classics",
                Description = "Best pop songs of all time",
                IsPublic = true,
                IsActive = false,
                IsAlbum = false,
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        await context.Playlists.AddRangeAsync(playlists);
        await context.SaveChangesAsync();

        // Add playlist ownership relationships
        var playlistOwners = new[]
        {
            new PlaylistOwner
            {
                PlaylistId = new Guid("99999999-9999-9999-9999-999999999999"), // Classic Rock Hits
                UserId = new Guid("11111111-1111-1111-1111-111111111111")      // John Lennon (owner)
            },
            new PlaylistOwner
            {
                PlaylistId = new Guid("88888888-8888-8888-8888-888888888888"), // Pop Classics
                UserId = new Guid("22222222-2222-2222-2222-222222222222")      // Paul McCartney (owner)
            }
        };

        await context.PlaylistOwners.AddRangeAsync(playlistOwners);
        await context.SaveChangesAsync();

        // Add playlist-song relationships
        var playlistSongs = new[]
        {
            new PlaylistSong
            {
                PlaylistId = new Guid("99999999-9999-9999-9999-999999999999"), // Classic Rock Hits
                SongId = new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),     // Imagine
                Order = 1,
                AddedAt = now
            },
            new PlaylistSong
            {
                PlaylistId = new Guid("99999999-9999-9999-9999-999999999999"), // Classic Rock Hits
                SongId = new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),     // Bohemian Rhapsody
                Order = 2,
                AddedAt = now
            },
            new PlaylistSong
            {
                PlaylistId = new Guid("88888888-8888-8888-8888-888888888888"), // Pop Classics
                SongId = new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"),     // Yesterday
                Order = 1,
                AddedAt = now
            }
        };

        await context.PlaylistSongs.AddRangeAsync(playlistSongs);
        await context.SaveChangesAsync();
    }
}