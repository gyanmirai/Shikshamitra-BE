using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ShikshaMitra.Seeders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmSeederContributor_Tests
{
	[Fact]
	public void Operational_Seed_Should_Use_Repaired_First_Run_Prerequisites()
	{
		var method = typeof(AdmissionCrmSeederContributor).GetMethod(
			"RepairOperationalScenariosAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);

		method.ShouldNotBeNull();
		var parameterTypes = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

		parameterTypes.ShouldContain(typeof(IReadOnlyDictionary<Guid, AdmissionInquiryLead>));
		parameterTypes.ShouldContain(typeof(IReadOnlyList<AdmissionCrmExamSlot>));
	}

	[Fact]
	public void Demo_Catalog_Should_Be_Focused_And_Employee_Assigned()
	{
		var leads = AdmissionCrmSeederContributor.DemoLeadSpecs();

		leads.Count.ShouldBe(10);
		leads.Select(item => item.Key).Distinct().Count().ShouldBe(10);
		leads.Select(item => item.StandardName).Distinct().OrderBy(item => item).ShouldBe(["Class 4", "Class 5"]);
		leads.Select(item => item.OwnerEmail).Distinct().OrderBy(item => item).ShouldBe(
		[
			"admissions-approver@ddvs.com",
			"admissions-counselor@ddvs.com",
			"admissions-reception@ddvs.com"
		]);
		leads.Single(item => item.Key == "rte-allotment").RouteType.ShouldBe(AdmissionCrmRouteType.RteEws);
		leads.Single(item => item.Key == "activation-repair").Priority.ShouldBe(AdmissionCrmLeadPriority.Hot);
	}

	[Fact]
	public void Demo_Catalog_Should_Cover_Each_Enrollment_Workspace_Without_Noise()
	{
		AdmissionCrmSeederContributor.FollowUpTaskSpecs().Count.ShouldBe(6);
		AdmissionCrmSeederContributor.ExamScenarioSpecs().Count.ShouldBe(4);
		AdmissionCrmSeederContributor.DemoRegistrationKeys().Count.ShouldBe(5);
		AdmissionCrmSeederContributor.DemoOfferKeys().Count.ShouldBe(3);
		AdmissionCrmSeederContributor.DemoActivationKeys().ShouldBe(["activation-repair"]);

		var activeKeys = AdmissionCrmSeederContributor.DemoLeadSpecs().Select(item => item.Key).ToHashSet();
		AdmissionCrmSeederContributor.FollowUpTaskSpecs().All(item => activeKeys.Contains(item.LeadKey)).ShouldBeTrue();
		AdmissionCrmSeederContributor.ExamScenarioSpecs().All(item => activeKeys.Contains(item.Key)).ShouldBeTrue();
		AdmissionCrmSeederContributor.DemoRegistrationKeys().All(activeKeys.Contains).ShouldBeTrue();
		AdmissionCrmSeederContributor.DemoOfferKeys().All(activeKeys.Contains).ShouldBeTrue();
		AdmissionCrmSeederContributor.DemoActivationKeys().All(activeKeys.Contains).ShouldBeTrue();
	}

	[Fact]
	public void Legacy_Reset_Catalog_Should_Include_Previous_And_Current_Deterministic_Leads()
	{
		var legacyKeys = AdmissionCrmSeederContributor.LegacyDemoLeadKeys();

		legacyKeys.ShouldContain("duplicate-warning");
		legacyKeys.ShouldContain("first-day-ready");
		AdmissionCrmSeederContributor.DemoLeadSpecs().All(item => legacyKeys.Contains(item.Key)).ShouldBeTrue();
	}

	[Fact]
	public void Demo_Identifiers_Should_Be_Deterministic_And_Tenant_Scoped()
	{
		var firstTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
		var secondTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

		AdmissionCrmSeederContributor.StableId(firstTenantId, "lead:new-inquiry")
			.ShouldBe(AdmissionCrmSeederContributor.StableId(firstTenantId, "lead:new-inquiry"));
		AdmissionCrmSeederContributor.StableId(firstTenantId, "lead:new-inquiry")
			.ShouldNotBe(AdmissionCrmSeederContributor.StableId(secondTenantId, "lead:new-inquiry"));
		AdmissionCrmSeederContributor.StableId(firstTenantId, "lead:new-inquiry")
			.ShouldNotBe(AdmissionCrmSeederContributor.StableId(firstTenantId, "lead:follow-up"));
	}

	[Fact]
	public void Demo_Reset_Should_Run_As_A_Versioned_One_Time_Step()
	{
		AdmissionCrmDemoScenarioSeederContributor.StepName.ShouldBe("AdmissionCrmDemoScenarioSeeder");
		AdmissionCrmDemoScenarioSeederContributor.StepVersion.ShouldBe("AdmissionCrmDemoScenarioSeeder:v2");
		AdmissionCrmDemoScenarioSeederContributor.StepOrder.ShouldBe(173);
	}
}
