using System.Net;
using System.Security.Claims;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Stores;

namespace Groovo.Services.Tus;

public class TusConfigurationFactory
{
    private const string TusEndpoint = "/files";
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TusConfigurationFactory> _logger;
    private readonly TusStorageConfiguration _storageConfig;

    public TusConfigurationFactory(
        IServiceProvider serviceProvider, 
        ILogger<TusConfigurationFactory> logger,
        TusStorageConfiguration storageConfig)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _storageConfig = storageConfig;
    }

    public DefaultTusConfiguration GetConfiguration()
    {
        return new DefaultTusConfiguration
        {
            UrlPath = TusEndpoint,
            Store = new TusDiskStore(_storageConfig.TempPath),
            MetadataParsingStrategy = MetadataParsingStrategy.AllowEmptyValues,
            Events = new Events
            {
                OnAuthorizeAsync = ctx =>
                {
                    var http = ctx.HttpContext;

                    // Reject unauthenticated users
                    if (!http.User.Identity?.IsAuthenticated ?? true)
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
