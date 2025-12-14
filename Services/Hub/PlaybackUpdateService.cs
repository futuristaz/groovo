using Microsoft.AspNetCore.SignalR;
using Groovo.DTOs;
using Groovo.Hubs;
using Groovo.Repositories;

namespace Groovo.Services.Hub;

public class PlaybackUpdateService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PlaybackUpdateService> _logger;
    private readonly IPlaybackStateStore<string, PlaybackState> _playbackStateStore;
    private readonly IHubContext<PlaylistHub> _hubContext;
    private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(3);

    public PlaybackUpdateService(
        IServiceProvider serviceProvider,
        ILogger<PlaybackUpdateService> logger,
        IPlaybackStateStore<string, PlaybackState> playbackStateStore,
        IHubContext<PlaylistHub> hubContext)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _playbackStateStore = playbackStateStore;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Playback Update Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdatePlaybackStates(stoppingToken);
                await Task.Delay(_updateInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in playback update service");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Playback Update Service stopped");
    }

    private async Task UpdatePlaybackStates(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var playlistRepository = scope.ServiceProvider.GetRequiredService<IPlaylistRepository>();
        var songRepository = scope.ServiceProvider.GetRequiredService<ISongRepository>();
        var shuffleService = scope.ServiceProvider.GetRequiredService<IShuffleService>();

        var activeStates = _playbackStateStore.GetAllActiveStates();

        foreach (var (playlistId, state) in activeStates)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                if (!state.IsPlaying || state.CurrentSongId == null || state.CurrentlyListening == 0)
                {
                    continue;
                }

                var timeSinceUpdate = (DateTime.UtcNow - state.LastUpdated).TotalSeconds;
                var newPosition = state.CurrentPosition + (int)timeSinceUpdate;

                if (newPosition >= state.CurrentLength)
                {
                    if (state.NextSongId.HasValue)
                    {
                        var nextSongLength = await songRepository.GetSongLengthAsync(state.NextSongId.Value);
                        if (nextSongLength > 0)
                        {
                            // Calculate following song based on shuffle state
                            Guid? followingSongId;
                            
                            if (state.IsShuffleEnabled && state.ShuffleSeed.HasValue)
                            {
                                followingSongId = await shuffleService.GetNextShuffledSongAsync(
                                    Guid.Parse(playlistId),
                                    state.NextSongId.Value,
                                    state.ShuffleSeed.Value
                                );
                            }
                            else
                            {
                                followingSongId = await playlistRepository.GetNextSongIdAsync(
                                    Guid.Parse(playlistId),
                                    state.NextSongId.Value
                                );
                            }
                            
                            var updatedState = _playbackStateStore.TryUpdate(playlistId, ps =>
                            {
                                ps.CurrentSongId = state.NextSongId.Value;
                                ps.CurrentPosition = 0;
                                ps.CurrentLength = nextSongLength;
                                ps.NextSongId = followingSongId;
                                ps.IsPlaying = true;
                                ps.LastUpdated = DateTime.UtcNow;
                                return ps;
                            });

                            await _hubContext.Clients.Group($"playlist_{playlistId}")
                                .SendAsync("PlaybackState", updatedState, stoppingToken);

                            _logger.LogInformation(
                                "Auto-advanced to next song in playlist {PlaylistId}: {SongId}",
                                playlistId,
                                updatedState.CurrentSongId
                            );
                        }
                        else
                        {
                            var stoppedState = _playbackStateStore.TryUpdate(playlistId, ps =>
                            {
                                ps.IsPlaying = false;
                                ps.CurrentPosition = ps.CurrentLength;
                                ps.LastUpdated = DateTime.UtcNow;
                                return ps;
                            });

                            await _hubContext.Clients.Group($"playlist_{playlistId}")
                                .SendAsync("PlaybackState", stoppedState, stoppingToken);
                        }
                    }
                    else
                    {
                        var stoppedState = _playbackStateStore.TryUpdate(playlistId, ps =>
                        {
                            ps.IsPlaying = false;
                            ps.CurrentPosition = ps.CurrentLength;
                            ps.LastUpdated = DateTime.UtcNow;
                            return ps;
                        });

                        await _hubContext.Clients.Group($"playlist_{playlistId}")
                            .SendAsync("PlaybackState", stoppedState, stoppingToken);
                    }
                }
                else
                {
                    var updatedState = _playbackStateStore.TryUpdate(playlistId, ps =>
                    {
                        ps.CurrentPosition = newPosition;
                        ps.LastUpdated = DateTime.UtcNow;
                        return ps;
                    });

                    await _hubContext.Clients.Group($"playlist_{playlistId}")
                        .SendAsync("PlaybackState", updatedState, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating playback state for playlist {PlaylistId}", playlistId);
            }
        }
    }
}
