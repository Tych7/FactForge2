using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FactForge.Data;

// Lets `dotnet ef migrations` construct a context without running the full app.
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(DbPath.GetConnectionString())
            .Options;
        return new AppDbContext(options);
    }
}
