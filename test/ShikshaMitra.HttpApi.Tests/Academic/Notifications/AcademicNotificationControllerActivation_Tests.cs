using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Controllers.Academic.Notifications;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

public class AcademicNotificationControllerActivation_Tests
{
	[Fact]
	public void Controller_activation_does_not_resolve_staff_application_services()
	{
		using var services = new ServiceCollection().BuildServiceProvider();

		ActivatorUtilities.CreateInstance<AcademicNotificationController>(services).ShouldNotBeNull();
	}
}
