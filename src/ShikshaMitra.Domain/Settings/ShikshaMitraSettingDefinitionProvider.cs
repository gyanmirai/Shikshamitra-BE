using Volo.Abp.Settings;
using ShikshaMitra.Communication.Notifications;

namespace ShikshaMitra.Settings;

public class ShikshaMitraSettingDefinitionProvider : SettingDefinitionProvider
{
	public override void Define(ISettingDefinitionContext context)
	{
		context.Add(new SettingDefinition("ShikshaMitra.HR.Reporting.TimeZone", "Asia/Kolkata", isVisibleToClients: false));
		context.Add(new SettingDefinition(NotificationSettingNames.SchoolTimeZone, "Asia/Kolkata", isVisibleToClients: false));
	}
}
