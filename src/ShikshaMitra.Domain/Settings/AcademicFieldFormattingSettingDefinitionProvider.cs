using ShikshaMitra.TenantSettings.AcademicSetups.Formatting;
using Volo.Abp.Settings;

namespace ShikshaMitra.Settings;

public class AcademicFieldFormattingSettingDefinitionProvider : SettingDefinitionProvider
{
	public override void Define(ISettingDefinitionContext context)
	{
		var definition = new SettingDefinition(AcademicFieldFormattingStore.SettingName, "{}", isVisibleToClients: false, isInherited: false);
		definition.Providers.Add(TenantSettingValueProvider.ProviderName);
		context.Add(definition);
	}
}
