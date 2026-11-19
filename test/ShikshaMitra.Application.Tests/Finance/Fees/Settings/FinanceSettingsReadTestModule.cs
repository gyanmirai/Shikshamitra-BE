using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.Data;
using Volo.Abp.Modularity;

namespace ShikshaMitra.Finance.Fees.Settings;

[DependsOn(typeof(ShikshaMitraApplicationTestModule))]
public class FinanceSettingsReadTestModule : AbpModule
{
	public override void ConfigureServices(ServiceConfigurationContext context)
	{
		// These fixtures seed their own settings. Keep real SQLite repositories, filters,
		// authorization and mapping, without unrelated whole-application sample data.
		context.Services.Replace(ServiceDescriptor.Singleton<IDataSeeder>(new SettingsReadTestSeeder()));
	}

	private sealed class SettingsReadTestSeeder : IDataSeeder
	{
		public Task SeedAsync(DataSeedContext context) => Task.CompletedTask;
	}
}
