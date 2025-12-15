using Groovo.Data.Contexts;
using System.Diagnostics.CodeAnalysis;

namespace Groovo.Data;

[ExcludeFromCodeCoverage]
public static class DatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // This application does not require any initial data to function
        // All data is created through the API endpoints
        await Task.CompletedTask;
    }
}