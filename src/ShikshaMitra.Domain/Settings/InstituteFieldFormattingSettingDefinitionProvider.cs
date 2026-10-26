using ShikshaMitra.TenantSettings.Formatting;
using Volo.Abp.Settings;

namespace ShikshaMitra.Settings;

public class InstituteFieldFormattingSettingDefinitionProvider : SettingDefinitionProvider
{
	public override void Define(ISettingDefinitionContext context)
	{
		foreach (var scope in InstituteFieldFormattingCatalog.Scopes)
		{
			var definition = new SettingDefinition(InstituteFieldFormattingStore.SettingPrefix + scope,
				isVisibleToClients: false, isInherited: false);
			definition.Providers.Add(TenantSettingValueProvider.ProviderName);
			context.Add(definition);
		}
	}
}
