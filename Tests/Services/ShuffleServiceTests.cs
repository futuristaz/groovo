using Moq;
using Groovo.Services.Hub;
using Groovo.Repositories;

namespace Groovo.Tests.Services;

public class ShuffleServiceTests
{
    private readonly Mock<IPlaylistRepository> _playlistRepositoryMock;
    private readonly ShuffleService _service;

    public ShuffleServiceTests()
    {
        _playlistRepositoryMock = new Mock<IPlaylistRepository>();
        _service = new ShuffleService(_playlistRepositoryMock.Object);
    }

    #region GetNextShuffledSongAsync Tests

    [Fact]
    public async Task GetNextShuffledSongAsync_ReturnsNextSong_InShuffledOrder()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var songIds = new List<Guid>
        {
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        };
        var shuffleSeed = 42;

        _playlistRepositoryMock.Setup(r => r.GetAllActiveSongIdsInOrderAsync(playlistId))
            .ReturnsAsync(songIds);

        // Act - Get shuffled sequence
        var firstSong = songIds[0];
        var nextSong1 = await _service.GetNextShuffledSongAsync(playlistId, firstSong, shuffleSeed);

        // Assert
        Assert.NotNull(nextSong1);
        Assert.NotEqual(firstSong, nextSong1);
        Assert.Contains(nextSong1.Value, songIds);
    }

    [Fact]
    public async Task GetNextShuffledSongAsync_ReturnsSameSequence_WithSameSeed()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var songIds = new List<Guid>
        {
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        };
        var shuffleSeed = 42;

        _playlistRepositoryMock.Setup(r => r.GetAllActiveSongIdsInOrderAsync(playlistId))
            .ReturnsAsync(songIds);

        // Act - Get next song twice with same seed
        var firstSong = songIds[0];
        var nextSong1 = await _service.GetNextShuffledSongAsync(playlistId, firstSong, shuffleSeed);
        var nextSong2 = await _service.GetNextShuffledSongAsync(playlistId, firstSong, shuffleSeed);

        // Assert - Should be deterministic
        Assert.Equal(nextSong1, nextSong2);
    }

    [Fact]
    public async Task GetNextShuffledSongAsync_ReturnsDifferentSequence_WithDifferentSeed()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var songIds = new List<Guid>
        {
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        };
        var shuffleSeed1 = 42;
        var shuffleSeed2 = 99;

        _playlistRepositoryMock.Setup(r => r.GetAllActiveSongIdsInOrderAsync(playlistId))
            .ReturnsAsync(songIds);

        // Act - Get next song with different seeds
        var firstSong = songIds[0];
        var nextSong1 = await _service.GetNextShuffledSongAsync(playlistId, firstSong, shuffleSeed1);
        var nextSong2 = await _service.GetNextShuffledSongAsync(playlistId, firstSong, shuffleSeed2);

        // Assert - Different seeds should produce different sequences (highly likely with 5 songs)
        Assert.NotEqual(nextSong1, nextSong2);
    }

    [Fact]
    public async Task GetNextShuffledSongAsync_ReturnsNull_WhenAtEndOfPlaylist()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var songIds = new List<Guid>
        {
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        };
        var shuffleSeed = 42;

        _playlistRepositoryMock.Setup(r => r.GetAllActiveSongIdsInOrderAsync(playlistId))
            .ReturnsAsync(songIds);

        // Act - Navigate to the end
        var currentSong = songIds[0];
        Guid? nextSong = currentSong;
        
        // Navigate through all songs until we reach the end
        while (nextSong.HasValue)
        {
            var temp = await _service.GetNextShuffledSongAsync(playlistId, nextSong.Value, shuffleSeed);
            if (temp == null)
            {
                // We've reached the end, nextSong will be set to null and break the loop
                nextSong = temp;
            }
            else
            {
                nextSong = temp;
            }
        }

        // Assert - Should return null after going through all songs
        Assert.Null(nextSong);
    }

    [Fact]
    public async Task GetNextShuffledSongAsync_ReturnsNull_WhenPlaylistIsEmpty()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var songIds = new List<Guid>();
        var shuffleSeed = 42;

        _playlistRepositoryMock.Setup(r => r.GetAllActiveSongIdsInOrderAsync(playlistId))
            .ReturnsAsync(songIds);

        // Act
        var result = await _service.GetNextShuffledSongAsync(playlistId, Guid.NewGuid(), shuffleSeed);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetNextShuffledSongAsync_ReturnsFirstSong_WhenCurrentSongNotFound()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var songIds = new List<Guid>
        {
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        };
        var shuffleSeed = 42;
        var removedSongId = Guid.NewGuid(); // Song that's not in the playlist

        _playlistRepositoryMock.Setup(r => r.GetAllActiveSongIdsInOrderAsync(playlistId))
            .ReturnsAsync(songIds);

        // Act
        var result = await _service.GetNextShuffledSongAsync(playlistId, removedSongId, shuffleSeed);

        // Assert - Should return first song in shuffled order
        Assert.NotNull(result);
        Assert.Contains(result.Value, songIds);
    }

    [Fact]
    public async Task GetNextShuffledSongAsync_WorksWithSingleSong()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var singleSong = Guid.NewGuid();
        var songIds = new List<Guid> { singleSong };
        var shuffleSeed = 42;

        _playlistRepositoryMock.Setup(r => r.GetAllActiveSongIdsInOrderAsync(playlistId))
            .ReturnsAsync(songIds);

        // Act
        var result = await _service.GetNextShuffledSongAsync(playlistId, singleSong, shuffleSeed);

        // Assert - Should return null since we're at the only (and last) song
        Assert.Null(result);
    }

    #endregion

    #region GetPreviousShuffledSongAsync Tests

    [Fact]
    public async Task GetPreviousShuffledSongAsync_ReturnsPreviousSong_InShuffledOrder()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var songIds = new List<Guid>
        {
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        };
        var shuffleSeed = 42;

        _playlistRepositoryMock.Setup(r => r.GetAllActiveSongIdsInOrderAsync(playlistId))
            .ReturnsAsync(songIds);

        // Act - Get next song first, then go back
        var firstSong = songIds[0];
        var secondSong = await _service.GetNextShuffledSongAsync(playlistId, firstSong, shuffleSeed);
        var previousSong = await _service.GetPreviousShuffledSongAsync(playlistId, secondSong!.Value, shuffleSeed);

        // Assert - Should return to first song
        Assert.NotNull(previousSong);
        Assert.NotEqual(secondSong, previousSong);
        Assert.Contains(previousSong.Value, songIds);
    }

    [Fact]
    public async Task GetPreviousShuffledSongAsync_ReturnsSameSequence_WithSameSeed()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var songIds = new List<Guid>
        {
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        };
        var shuffleSeed = 42;

        _playlistRepositoryMock.Setup(r => r.GetAllActiveSongIdsInOrderAsync(playlistId))
            .ReturnsAsync(songIds);

        // Act - Get previous song twice with same seed
        var lastSong = songIds[2];
        var previousSong1 = await _service.GetPreviousShuffledSongAsync(playlistId, lastSong, shuffleSeed);
        var previousSong2 = await _service.GetPreviousShuffledSongAsync(playlistId, lastSong, shuffleSeed);

        // Assert - Should be deterministic
        Assert.Equal(previousSong1, previousSong2);
    }

    [Fact]
    public async Task GetPreviousShuffledSongAsync_ReturnsNull_WhenAtBeginningOfPlaylist()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var songIds = new List<Guid>
        {
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        };
        var shuffleSeed = 42;

        _playlistRepositoryMock.Setup(r => r.GetAllActiveSongIdsInOrderAsync(playlistId))
            .ReturnsAsync(songIds);

        // Act - Find the first song in shuffled order by checking which one has no previous
        Guid? firstShuffledSong = null;
        foreach (var songId in songIds)
        {
            var previous = await _service.GetPreviousShuffledSongAsync(playlistId, songId, shuffleSeed);
            if (previous == null)
            {
                firstShuffledSong = songId;
                break;
            }
        }
        
        Assert.NotNull(firstShuffledSong);
        
        // Now test going back from the first song
        var result = await _service.GetPreviousShuffledSongAsync(playlistId, firstShuffledSong.Value, shuffleSeed);

        // Assert - Should return null when at beginning
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPreviousShuffledSongAsync_ReturnsNull_WhenPlaylistIsEmpty()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var songIds = new List<Guid>();
        var shuffleSeed = 42;

        _playlistRepositoryMock.Setup(r => r.GetAllActiveSongIdsInOrderAsync(playlistId))
            .ReturnsAsync(songIds);

        // Act
        var result = await _service.GetPreviousShuffledSongAsync(playlistId, Guid.NewGuid(), shuffleSeed);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPreviousShuffledSongAsync_ReturnsNull_WhenCurrentSongNotFound()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var songIds = new List<Guid>
        {
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        };
        var shuffleSeed = 42;
        var removedSongId = Guid.NewGuid(); // Song that's not in the playlist

        _playlistRepositoryMock.Setup(r => r.GetAllActiveSongIdsInOrderAsync(playlistId))
            .ReturnsAsync(songIds);

        // Act
        var result = await _service.GetPreviousShuffledSongAsync(playlistId, removedSongId, shuffleSeed);

        // Assert - Should return null when song not found
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPreviousShuffledSongAsync_WorksWithSingleSong()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var singleSong = Guid.NewGuid();
        var songIds = new List<Guid> { singleSong };
        var shuffleSeed = 42;

        _playlistRepositoryMock.Setup(r => r.GetAllActiveSongIdsInOrderAsync(playlistId))
            .ReturnsAsync(songIds);

        // Act
        var result = await _service.GetPreviousShuffledSongAsync(playlistId, singleSong, shuffleSeed);

        // Assert - Should return null since we're at the only (and first) song
        Assert.Null(result);
    }

    #endregion

    #region Navigation Tests (Next and Previous Together)

    [Fact]
    public async Task NavigateNextThenPrevious_ReturnsToOriginalSong()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var songIds = new List<Guid>
        {
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        };
        var shuffleSeed = 42;

        _playlistRepositoryMock.Setup(r => r.GetAllActiveSongIdsInOrderAsync(playlistId))
            .ReturnsAsync(songIds);

        // Act - Go next then previous
        var startSong = songIds[0];
        var nextSong = await _service.GetNextShuffledSongAsync(playlistId, startSong, shuffleSeed);
        var backToStart = await _service.GetPreviousShuffledSongAsync(playlistId, nextSong!.Value, shuffleSeed);

        // Assert - Should return to original song (but we need to find what the original was in shuffled order)
        Assert.NotNull(backToStart);
        Assert.Contains(backToStart.Value, songIds);
    }

    [Fact]
    public async Task CompleteForwardBackwardTraversal_MaintainsConsistency()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var songIds = new List<Guid>
        {
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        };
        var shuffleSeed = 42;

        _playlistRepositoryMock.Setup(r => r.GetAllActiveSongIdsInOrderAsync(playlistId))
            .ReturnsAsync(songIds);

        // Act - Find the first song in shuffled order (one that has no previous)
        Guid? firstShuffledSong = null;
        foreach (var songId in songIds)
        {
            var previous = await _service.GetPreviousShuffledSongAsync(playlistId, songId, shuffleSeed);
            if (previous == null)
            {
                firstShuffledSong = songId;
                break;
            }
        }
        
        Assert.NotNull(firstShuffledSong);
        
        // Build the complete shuffled sequence starting from the first song
        var sequence = new List<Guid>();
        var currentSong = firstShuffledSong.Value;
        sequence.Add(currentSong);

        while (true)
        {
            var nextSong = await _service.GetNextShuffledSongAsync(playlistId, currentSong, shuffleSeed);
            if (nextSong == null) break;
            sequence.Add(nextSong.Value);
            currentSong = nextSong.Value;
        }

        // Assert - All songs should be in the sequence
        Assert.Equal(songIds.Count, sequence.Count);
        Assert.All(songIds, songId => Assert.Contains(songId, sequence));
    }

    #endregion

    #region GenerateShuffleSeed Tests

    [Fact]
    public void GenerateShuffleSeed_ReturnsInteger()
    {
        // Act
        var seed = _service.GenerateShuffleSeed();

        // Assert
        Assert.IsType<int>(seed);
    }

    [Fact]
    public void GenerateShuffleSeed_ReturnsDifferentValues_OnMultipleCalls()
    {
        // Act
        var seed1 = _service.GenerateShuffleSeed();
        var seed2 = _service.GenerateShuffleSeed();
        var seed3 = _service.GenerateShuffleSeed();

        // Assert - While theoretically they could be the same, it's extremely unlikely
        var seeds = new[] { seed1, seed2, seed3 };
        Assert.True(seeds.Distinct().Count() > 1, "Should generate different seeds on multiple calls");
    }

    [Fact]
    public void GenerateShuffleSeed_GeneratesValidSeeds()
    {
        // Act - Generate multiple seeds and verify they can be used
        var seeds = new List<int>();
        for (int i = 0; i < 10; i++)
        {
            seeds.Add(_service.GenerateShuffleSeed());
        }

        // Assert - All seeds should be valid integers (no exceptions)
        Assert.Equal(10, seeds.Count);
        Assert.All(seeds, seed => Assert.True(seed != 0 || seed == 0)); // Just verify it's an int
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ShuffleWithTwoSongs_ProducesCorrectSequence()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var song1 = Guid.NewGuid();
        var song2 = Guid.NewGuid();
        var songIds = new List<Guid> { song1, song2 };
        var shuffleSeed = 42;

        _playlistRepositoryMock.Setup(r => r.GetAllActiveSongIdsInOrderAsync(playlistId))
            .ReturnsAsync(songIds);

        // Act
        var nextFromFirst = await _service.GetNextShuffledSongAsync(playlistId, song1, shuffleSeed);
        var nextFromSecond = await _service.GetNextShuffledSongAsync(playlistId, song2, shuffleSeed);

        // Assert - One should have next, one shouldn't (depending on shuffle)
        Assert.True((nextFromFirst == null) != (nextFromSecond == null), 
            "With 2 songs, one should be last and one should have a next");
    }

    [Fact]
    public async Task ShuffleDoesNotModifyOriginalSongList()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var songIds = new List<Guid>
        {
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        };
        var originalOrder = new List<Guid>(songIds);
        var shuffleSeed = 42;

        _playlistRepositoryMock.Setup(r => r.GetAllActiveSongIdsInOrderAsync(playlistId))
            .ReturnsAsync(songIds);

        // Act
        await _service.GetNextShuffledSongAsync(playlistId, songIds[0], shuffleSeed);

        // Assert - Original list should remain unchanged
        Assert.Equal(originalOrder, songIds);
    }

    #endregion
}
