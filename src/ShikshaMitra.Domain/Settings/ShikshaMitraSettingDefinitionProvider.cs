using Volo.Abp.Settings;

namespace ShikshaMitra.Settings;

public class ShikshaMitraSettingDefinitionProvider : SettingDefinitionProvider
{
	public override void Define(ISettingDefinitionContext context)
	{
		context.Add(new SettingDefinition("ShikshaMitra.HR.Reporting.TimeZone", "Asia/Kolkata", isVisibleToClients: false));
	}
}
