using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AttandenceDesktop.Data;

/// <summary>
/// Provides ApplicationDbContext instance at design-time (for dotnet-ef commands).
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        var dbPath = Path.Combine(AppContext.BaseDirectory, "TimeAttendance.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
        return new ApplicationDbContext(optionsBuilder.Options);
    }
} 