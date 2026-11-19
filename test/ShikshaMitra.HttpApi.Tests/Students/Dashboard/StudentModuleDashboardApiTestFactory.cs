using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Seeders;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.Dashboard;

[CollectionDefinition(Name)]
public sealed class StudentModuleDashboardApiTestCollection : ICollectionFixture<StudentModuleDashboardApiTestFactory>
{
	public const string Name = "Student module dashboard API";
}

public sealed class StudentModuleDashboardApiTestFactory : ShikshaMitraApiTestFactory
{
	public StudentDashboardQueryCounter Counter { get; } = new();
	protected override void ConfigureTestDatabase(IServiceCollection services)
	{
		base.ConfigureTestDatabase(services);
		services.Configure<AbpDbContextOptions>(options => options.PreConfigure(context => context.DbContextOptions.AddInterceptors(Counter)));
	}
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
		{
			[ShikshaMitraSeederProfile.ContextPropertyName] = ShikshaMitraSeederProfile.Minimal
		}));
	}
}
