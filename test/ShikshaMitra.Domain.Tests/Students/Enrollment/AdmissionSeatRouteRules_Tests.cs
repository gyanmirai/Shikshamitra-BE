using System;
using System.Collections.Generic;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionSeatRouteRules_Tests
{
	[Theory]
	[InlineData("waitlisted")]
	[InlineData("released")]
	[InlineData("expired")]
	public void Non_confirmation_route_must_depend_on_recorded_seat_status(string status)
	{
		var facts = new Dictionary<string, string> { ["seat.status"] = status };
		Should.Throw<BusinessException>(() => AdmissionSeatRouteRules.EnsureRoute(status, DefaultRoute(), facts));
		var matching = $"{{\"field\":\"seat.status\",\"operator\":\"Equals\",\"value\":\"{status}\"}}";
		AdmissionSeatRouteRules.EnsureRoute(status, Conditional(matching), facts);
	}

	[Fact]
	public void Confirmed_seat_can_use_normal_route() =>
		AdmissionSeatRouteRules.EnsureRoute("confirmed", null,
			new Dictionary<string, string> { ["seat.status"] = "confirmed" });

	private static AdmissionWorkflowTransitionDefinition DefaultRoute() =>
		new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0, isDefault: true);
	private static AdmissionWorkflowTransitionDefinition Conditional(string json) =>
		new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0, json);
}
