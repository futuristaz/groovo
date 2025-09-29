using Groovo.Filters;

namespace Groovo;

public class Program
{
  public static void Main(string[] args)
  {

    var builder = WebApplication.CreateBuilder(args);

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
      c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "Latest version", Version = "v1" });
    });
    builder.Services.AddVersionedApiExplorer(options =>
    {
      options.GroupNameFormat = "'v'VVV";
      options.SubstituteApiVersionInUrl = true;
    });

    //builder.Services.AddScoped<ITaskService, TaskService>();

    var app = builder.Build();

    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapControllers();

    app.Run();
  }
}