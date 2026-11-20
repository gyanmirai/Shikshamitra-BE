using Volo.Abp.Settings;

namespace ShikshaMitra.Library.Policies;

public class LibraryPolicySettingDefinitionProvider : SettingDefinitionProvider
{
	public override void Define(ISettingDefinitionContext context) =>
		context.Add(new SettingDefinition(LibraryPolicySettingNames.CalendarTimeZone, isVisibleToClients: false));
}
