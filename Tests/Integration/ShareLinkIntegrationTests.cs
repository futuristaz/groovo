using Groovo.Data.Contexts;
using Groovo.Models;
using Groovo.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Groovo.DTOs;

namespace Tests.Integration;

/// <summary>
/// Integration tests for the Share Link Module.
/// Covers the create/resolve round-trip, permission enforcement,
/// and revocation behaviour.
/// </summary>
public class ShareLinkIntegrationTests : IClassFixture<GroovoWebFactory>
{
    private readonly GroovoWebFactory _factory;

    public ShareLinkIntegrationTests(GroovoWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ShareLink_CreatedAndResolved_ReturnsSameSong()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var shareLinkService = scope.ServiceProvider.GetRequiredService<IShareLinkService>();

        db.ShareLinks.RemoveRange(db.ShareLinks);
        db.SongAuthors.RemoveRange(db.SongAuthors);
        db.PlaylistOwners.RemoveRange(db.PlaylistOwners);
        db.Songs.RemoveRange(db.Songs);
        db.Playlists.RemoveRange(db.Playlists);
        db.Users.RemoveRange(db.Users);
        await db.SaveChangesAsync();

        // Arrange
        var author = new User
        {
            Id           = Guid.NewGuid(),
            Name         = "Author One",
            Email        = "author1@test.com",
            Role         = UserRole.Author,
            PasswordHash = "hash"
        };
        var album = new Playlist
        {
            Id       = Guid.NewGuid(),
            Name     = "Public Album",
            IsActive = true,
            IsPublic = true,
            IsAlbum  = true
        };
        var song = new Song
        {
            Id          = Guid.NewGuid(),
            Name        = "My Track",
            Album       = album.Id,
            AudioUrl    = "audio/track.mp3",
            ReleaseDate = DateTime.UtcNow,
            IsActive    = true
        };
        db.Users.Add(author);
        db.Playlists.Add(album);
        db.Songs.Add(song);
        db.SongAuthors.Add(new SongAuthor       { SongId = song.Id,     UserId = author.Id });
        db.PlaylistOwners.Add(new PlaylistOwner { PlaylistId = album.Id, UserId = author.Id });
        await db.SaveChangesAsync();

        // Act — create then resolve
        var (created, _) = await shareLinkService.CreateShareLinkAsync(
            ShareResourceType.Track, song.Id, author.Id, "Author", "https://groovo.test");

        var resolved = await shareLinkService.ResolveShareLinkAsync(created!.Token);

        // Assert
        Assert.NotNull(resolved);
        Assert.Equal(ShareResourceType.Track, resolved!.ResourceType);
        Assert.Equal(song.Id, resolved.Track!.Id);
    }

    [Fact]
    public async Task CreateShareLink_PrivateTrack_RegularUser_ReturnsErrorAndCreatesNoLink()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var shareLinkService = scope.ServiceProvider.GetRequiredService<IShareLinkService>();

        db.ShareLinks.RemoveRange(db.ShareLinks);
        db.SongAuthors.RemoveRange(db.SongAuthors);
        db.PlaylistOwners.RemoveRange(db.PlaylistOwners);
        db.Songs.RemoveRange(db.Songs);
        db.Playlists.RemoveRange(db.Playlists);
        db.Users.RemoveRange(db.Users);
        await db.SaveChangesAsync();

        // Arrange — private album owned by an author
        var author = new User
        {
            Id           = Guid.NewGuid(),
            Name         = "Author Two",
            Email        = "author2@test.com",
            Role         = UserRole.Author,
            PasswordHash = "hash"
        };
        var album = new Playlist
        {
            Id       = Guid.NewGuid(),
            Name     = "Private Album",
            IsActive = true,
            IsPublic = false,  // ← private
            IsAlbum  = true
        };
        var song = new Song
        {
            Id          = Guid.NewGuid(),
            Name        = "Secret Track",
            Album       = album.Id,
            AudioUrl    = "audio/secret.mp3",
            ReleaseDate = DateTime.UtcNow,
            IsActive    = true
        };
        var regularUser = new User
        {
            Id           = Guid.NewGuid(),
            Name         = "Regular",
            Email        = "regular@test.com",
            Role         = UserRole.User,
            PasswordHash = "hash"
        };
        db.Users.AddRange(author, regularUser);
        db.Playlists.Add(album);
        db.Songs.Add(song);
        db.SongAuthors.Add(new SongAuthor       { SongId = song.Id,     UserId = author.Id });
        db.PlaylistOwners.Add(new PlaylistOwner { PlaylistId = album.Id, UserId = author.Id });
        await db.SaveChangesAsync();

        // Act — regular user tries to share a private track
        var (shareLink, errorMessage) = await shareLinkService.CreateShareLinkAsync(
            ShareResourceType.Track, song.Id, regularUser.Id, "User", "https://groovo.test");

        // Assert — rejected, nothing persisted
        Assert.Null(shareLink);
        Assert.NotNull(errorMessage);

        var linksInDb = await db.ShareLinks.CountAsync();
        Assert.Equal(0, linksInDb);
    }

    [Fact]
    public async Task RevokedShareLink_CannotBeResolved()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var shareLinkService = scope.ServiceProvider.GetRequiredService<IShareLinkService>();

        db.ShareLinks.RemoveRange(db.ShareLinks);
        db.SongAuthors.RemoveRange(db.SongAuthors);
        db.PlaylistOwners.RemoveRange(db.PlaylistOwners);
        db.Songs.RemoveRange(db.Songs);
        db.Playlists.RemoveRange(db.Playlists);
        db.Users.RemoveRange(db.Users);
        await db.SaveChangesAsync();

        // Arrange — public album and song
        var author = new User
        {
            Id           = Guid.NewGuid(),
            Name         = "Author Three",
            Email        = "author3@test.com",
            Role         = UserRole.Author,
            PasswordHash = "hash"
        };
        var album = new Playlist
        {
            Id       = Guid.NewGuid(),
            Name     = "Another Public Album",
            IsActive = true,
            IsPublic = true,
            IsAlbum  = true
        };
        var song = new Song
        {
            Id          = Guid.NewGuid(),
            Name        = "Public Track",
            Album       = album.Id,
            AudioUrl    = "audio/public.mp3",
            ReleaseDate = DateTime.UtcNow,
            IsActive    = true
        };
        db.Users.Add(author);
        db.Playlists.Add(album);
        db.Songs.Add(song);
        db.SongAuthors.Add(new SongAuthor       { SongId = song.Id,     UserId = author.Id });
        db.PlaylistOwners.Add(new PlaylistOwner { PlaylistId = album.Id, UserId = author.Id });
        await db.SaveChangesAsync();

        // Create then revoke
        var (created, _) = await shareLinkService.CreateShareLinkAsync(
            ShareResourceType.Track, song.Id, author.Id, "Author", "https://groovo.test");

        Assert.NotNull(created);
        await shareLinkService.RevokeShareLinkAsync(created!.ShareLinkId, author.Id, "Author");

        // Act — attempt to resolve the revoked link
        var resolved = await shareLinkService.ResolveShareLinkAsync(created.Token);

        // Assert — null because link is revoked
        Assert.Null(resolved);
    }
}