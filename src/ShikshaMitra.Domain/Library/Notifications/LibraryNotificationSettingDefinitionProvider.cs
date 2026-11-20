using Volo.Abp.Settings;

namespace ShikshaMitra.Library.Notifications;

public class LibraryNotificationSettingDefinitionProvider : SettingDefinitionProvider
{
	public override void Define(ISettingDefinitionContext context) => context.Add(
		new SettingDefinition(LibraryNotificationSettingNames.AutomaticReminderTimeLocal, isVisibleToClients: false),
		new SettingDefinition(LibraryNotificationSettingNames.OperationalRecipientUserIds, isVisibleToClients: false),
		new SettingDefinition(LibraryNotificationSettingNames.StudentInboxRouting, isVisibleToClients: false));
}
