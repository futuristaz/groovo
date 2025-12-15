using System.Net;
using System.Security.Claims;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Stores;

namespace Groovo.Services.Tus;

public class TusConfigurationFactory
{
    private readonly string _tusEndpoint;
    private readonly ILogger<TusConfigurationFactory> _logger;
    private readonly TusStorageConfiguration _storageConfig;

    public TusConfigurationFactory(
        ILogger<TusConfigurationFactory> logger,
        TusStorageConfiguration storageConfig,
        IConfiguration configuration)
    {
        _logger = logger;
        _storageConfig = storageConfig;
        var pathBase = configuration["PathBase"] ?? string.Empty;
        _tusEndpoint = string.IsNullOrEmpty(pathBase) ? "/files" : $"{pathBase}/files";
    }

    public DefaultTusConfiguration GetConfiguration()
    {
        return new DefaultTusConfiguration
        {
            UrlPath = _tusEndpoint,
            Store = new TusDiskStore(_storageConfig.TempPath),
            MetadataParsingStrategy = MetadataParsingStrategy.AllowEmptyValues,
            Events = new Events
            {
                OnAuthorizeAsync = ctx =>
                {
                    var http = ctx.HttpContext;

                    // Reject unauthenticated users
                    if (!(http.User.Identity?.IsAuthenticated ?? false))
                    {
                        ctx.FailRequest(HttpStatusCode.Unauthorized, "Authentication is required.");
                    }

                    return Task.CompletedTask;
                },

                OnFileCompleteAsync = async eventContext =>
                {
                    var file = await eventContext.GetFileAsync();
                    var metadata = await file.GetMetadataAsync(eventContext.CancellationToken);

                    _logger.LogInformation(
                        "File upload completed. Upload ID: {UploadId}, Filename: {Filename}",
                        file.Id,
                        metadata.TryGetValue("filename", out var filename) ? filename.GetString(System.Text.Encoding.UTF8) : "unknown"
                    );
                }
            }
        };
    }
}
