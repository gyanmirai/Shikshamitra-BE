using System.Collections.Generic;
using System.Linq;
using System;
using ShikshaMitra.Communication.Notifications;
using Shouldly;
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
}
