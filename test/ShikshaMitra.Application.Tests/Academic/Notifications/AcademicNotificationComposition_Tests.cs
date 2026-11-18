using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using Shouldly;
using Volo.Abp.Settings;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

public class AcademicNotificationComposition_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public void Application_container_can_construct_the_recipient_boundary()
	{
		GetRequiredService<IAcademicNotificationRecipientResolver>().ShouldNotBeNull();
	}

	[Fact]
	public void Authoring_facade_is_registered_at_the_authenticated_application_boundary()
	{
		GetRequiredService<IServiceProvider>().GetService(typeof(INotificationTemplateAppService)).ShouldNotBeNull();
	}

	[Fact]
	public void Generic_engine_receives_the_academic_event_schema_provider()
	{
		GetRequiredService<IEnumerable<INotificationEventDefinitionProvider>>()
			.SelectMany(x => x.GetAll()).ShouldContain(x => x.EventKey == "Academic.Homework.Published");
	}

	[Fact]
	public async Task Runtime_configuration_services_and_the_server_only_school_clock_are_registered()
	{
		GetRequiredService<NotificationRuleConfigurationCodec>().ShouldNotBeNull();
		GetRequiredService<NotificationEffectiveRuleResolver>().ShouldNotBeNull();
		GetRequiredService<NotificationTemplateSnapshotSelector>().ShouldNotBeNull();
		GetRequiredService<NotificationIntentSnapshotCodec>().ShouldNotBeNull();
		GetRequiredService<INotificationIntentWriter>().ShouldNotBeNull();
		GetRequiredService<NotificationIntentSourceReader>().ShouldNotBeNull();
		GetRequiredService<NotificationDispatchPolicy>().ShouldNotBeNull();
		GetRequiredService<NotificationDispatchPayloadProtector>().ShouldNotBeNull();
		GetRequiredService<NotificationDispatchPlanner>().ShouldNotBeNull();
		GetRequiredService<IEnumerable<INotificationRuntimeRecipientResolver>>().Count(x => x.Supports("Academic.Homework.Published")).ShouldBe(1);
		GetRequiredService<NotificationSchoolTimeZone>().ShouldNotBeNull();
		var setting = await GetRequiredService<ISettingDefinitionManager>().GetAsync(NotificationSettingNames.SchoolTimeZone);
		setting.IsVisibleToClients.ShouldBeFalse(); setting.DefaultValue.ShouldBe("Asia/Kolkata");
	}
}
