using System.Linq;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Employees.Onboarding;
using ShikshaMitra.Infrastructure.Traits;
using Xunit;

namespace ShikshaMitra.HR.Employees;

public class EmployeeWorkAssignmentControllerDiscovery_Tests
{
	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public void Internal_work_assignment_validator_has_no_conventional_http_actions()
	{
		using var factory = new DiscoveryFactory();
		var actions = factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>()
			.ActionDescriptors.Items.OfType<ControllerActionDescriptor>().ToList();
		// Prove discovery is populated, then reject every action on the internal helper.
		Assert.Contains(actions, action => action.ControllerName == "EmployeeApplication");
		Assert.DoesNotContain(actions,
			action => action.ControllerTypeInfo.AsType() == typeof(EmployeeWorkAssignmentValidator));
	}

	private sealed class DiscoveryFactory : ShikshaMitraApiTestFactory
	{
		protected override bool InitializeTestData => false;
	}
}
