using System;
using System.Collections.Generic;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionOfferRouteRules_Tests
{
	[Theory]
	[InlineData("declined")]
	[InlineData("expired")]
	[InlineData("withdrawn")]
	public void Non_acceptance_route_must_depend_on_the_recorded_offer_status(string status)
	{
		var facts = new Dictionary<string, string>
		{
			["offer.status"] = status,
			["application.status"] = "Offered"
		};
		Should.Throw<BusinessException>(() => AdmissionOfferRouteRules.EnsureRoute(status, DefaultRoute(), facts));
		Should.Throw<BusinessException>(() => AdmissionOfferRouteRules.EnsureRoute(status, null, facts));

		var matching = "{\"field\":\"offer.status\",\"operator\":\"Equals\",\"value\":\"" + status + "\"}";
		var generic = "{\"field\":\"application.status\",\"operator\":\"Equals\",\"value\":\"Offered\"}";
		AdmissionOfferRouteRules.EnsureRoute(status, Conditional(matching), facts);
		AdmissionOfferRouteRules.EnsureRoute(status,
			Conditional("{\"operator\":\"ALL\",\"conditions\":[" + matching + "," + generic + "]}"), facts);
		Should.Throw<BusinessException>(() => AdmissionOfferRouteRules.EnsureRoute(status,
			Conditional("{\"operator\":\"ANY\",\"conditions\":[" + matching + "," + generic + "]}"), facts));
	}

	[Fact]
	public void Accepted_offer_can_use_the_normal_route()
	{
		AdmissionOfferRouteRules.EnsureRoute("accepted", null,
			new Dictionary<string, string> { ["offer.status"] = "accepted" });
	}

	private static AdmissionWorkflowTransitionDefinition DefaultRoute() =>
		new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0, isDefault: true);

	private static AdmissionWorkflowTransitionDefinition Conditional(string json) =>
		new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0, json);
}
