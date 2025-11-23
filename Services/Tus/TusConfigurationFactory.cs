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
                OnFileCompleteAsync = async eventContext =>
                {
                    var file = await eventContext.GetFileAsync();
                    var metadata = await file.GetMetadataAsync(eventContext.CancellationToken);

                    _logger.LogInformation(
                        "File upload completed. Upload ID: {UploadId}, Filename: {Filename}",
                        file.Id,
                        metadata.TryGetValue("filename", out var filename) ? filename.GetString(System.Text.Encoding.UTF8) : "unknown"
                    );

                    // Do not move file here - movement happens after form submission
                }
            }
        };
    }
}
