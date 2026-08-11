using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
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
	public void Default_Registration_Template_Should_Use_Semantic_Field_Types()
	{
		using var document = JsonDocument.Parse(AdmissionCrmSeederContributor.DefaultRegistrationSectionsJson());
		var fieldTypes = document.RootElement
			.EnumerateArray()
			.SelectMany(section => section.GetProperty("fields").EnumerateArray())
			.ToDictionary(
				field => field.GetProperty("key").GetString()!,
				field => (AdmissionCrmFormFieldType)field.GetProperty("fieldType").GetInt32());

		fieldTypes["firstName"].ShouldBe(AdmissionCrmFormFieldType.Text);
		fieldTypes["dateOfBirth"].ShouldBe(AdmissionCrmFormFieldType.Date);
		fieldTypes["parentName"].ShouldBe(AdmissionCrmFormFieldType.Text);
		fieldTypes["phone"].ShouldBe(AdmissionCrmFormFieldType.Phone);
	}

	[Fact]
	public void Default_Route_Catalog_Should_Cover_Routine_Quick_Inquiry_Pathways()
	{
		var routes = AdmissionCrmSeederContributor.DefaultRouteSpecs();

		routes.Select(item => item.RouteType).ShouldBe(
		[
			AdmissionCrmRouteType.Fresh,
			AdmissionCrmRouteType.SiblingPriority,
			AdmissionCrmRouteType.Lateral
		]);
		routes.Select(item => item.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(routes.Count);
		routes.Select(item => item.Name).ShouldBe(["Fresh admission", "Sibling priority", "Lateral entry"]);

		using var freshConfiguration = JsonDocument.Parse(routes[0].ConfigurationJson);
		freshConfiguration.RootElement.GetProperty("requiresPolicyEvaluation").GetBoolean().ShouldBeFalse();
		using var siblingConfiguration = JsonDocument.Parse(routes[1].ConfigurationJson);
		siblingConfiguration.RootElement
			.GetProperty("requiredEvidenceCodes")
			.EnumerateArray()
			.Select(item => item.GetString())
			.ShouldContain("SIBLING_RELATIONSHIP");
	}

	[Fact]
	public void Default_Routes_Should_Be_Effective_During_Pre_Opening_Inquiry_Capture()
	{
		var now = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		var (effectiveFrom, effectiveUntil) = AdmissionCrmSeederContributor.DefaultRouteEffectivePeriod(
			new DateTime(2026, 6, 1),
			new DateTime(2027, 5, 31),
			now);
		var definition = new AdmissionCrmRouteDefinition(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			"FRESH",
			"Fresh admission",
			AdmissionCrmRouteType.Fresh,
			1,
			"{}",
			effectiveFrom,
			effectiveUntil);
		definition.Submit(Guid.NewGuid());
		definition.Publish(Guid.NewGuid());

		effectiveFrom.ShouldBe(now.Date);
		effectiveUntil.ShouldBe(new DateTime(2027, 6, 1, 0, 0, 0, DateTimeKind.Utc));
		definition.IsEffectiveAt(now).ShouldBeTrue();
	}

	[Fact]
	public void Default_Route_Publication_Should_Use_Only_Explicit_Authorized_Personas()
	{
		var makerUserId = Guid.NewGuid();
		var approverUserId = Guid.NewGuid();
		var arbitraryUserId = Guid.NewGuid();
		var users = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
		{
			["admissions-counselor@ddvs.com"] = makerUserId,
			["admissions-approver@ddvs.com"] = approverUserId,
			["unrelated-admin@ddvs.com"] = arbitraryUserId
		};

		AdmissionCrmSeederContributor.ResolveRoutePublicationUserIds(users)
			.ShouldBe([makerUserId, approverUserId]);

		users.Remove("admissions-counselor@ddvs.com");
		AdmissionCrmSeederContributor.ResolveRoutePublicationUserIds(users).ShouldBeEmpty();
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
	public void Demo_Reset_Catalog_Should_Contain_Only_Current_Deterministic_Leads()
	{
		AdmissionCrmSeederContributor.DemoLeadKeys()
			.ShouldBe(AdmissionCrmSeederContributor.DemoLeadSpecs().Select(item => item.Key).ToArray());
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
