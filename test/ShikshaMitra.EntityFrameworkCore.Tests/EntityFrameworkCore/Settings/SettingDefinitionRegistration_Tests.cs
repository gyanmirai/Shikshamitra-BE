using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Settings;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Settings;

public class SettingDefinitionRegistration_Tests : ShikshaMitraEntityFrameworkCoreTestBase
{
	[Fact]
	public async Task Setting_Definitions_Should_Have_Unique_Names()
	{
		var settingDefinitionManager = ServiceProvider.GetRequiredService<ISettingDefinitionManager>();

		var duplicateNames = (await settingDefinitionManager.GetAllAsync())
			.GroupBy(definition => definition.Name)
			.Where(group => group.Count() > 1)
			.Select(group => group.Key)
			.ToList();

		duplicateNames.ShouldBeEmpty();
	}
}
