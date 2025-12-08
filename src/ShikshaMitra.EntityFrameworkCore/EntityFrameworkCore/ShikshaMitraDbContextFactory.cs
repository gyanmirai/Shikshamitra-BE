using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ShikshaMitra.EntityFrameworkCore;

/* This class is needed for EF Core console commands
 * (like Add-Migration and Update-Database commands) */
public class ShikshaMitraDbContextFactory : IDesignTimeDbContextFactory<ShikshaMitraDbContext>
{
    public ShikshaMitraDbContext CreateDbContext(string[] args)
    {
        // https://www.npgsql.org/efcore/release-notes/6.0.html#opting-out-of-the-new-timestamp-mapping-logic
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", isEnabled: false);

        ShikshaMitraEfCoreEntityExtensionMappings.Configure();

        var configuration = ShikshaMitraConfigurationHelper.BuildConfiguration();

        var builder = new DbContextOptionsBuilder<ShikshaMitraDbContext>()
            .UseNpgsql(configuration.GetConnectionString("Default"));

        return new ShikshaMitraDbContext(builder.Options);
    }
}
