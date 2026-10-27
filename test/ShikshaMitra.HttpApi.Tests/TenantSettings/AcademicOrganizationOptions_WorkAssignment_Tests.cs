using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Onboarding;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public partial class AcademicOrganizationOptionsApi_Tests
{
	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task WorkAssignment_Invite_Uses_Organization_Catalogue_And_Preserves_Multiple_Labels()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var seeded = await SeedAcademicOrganizationsAsync();
		var response = await Client.PostAsJsonAsync("/api/employee-applications", new CreateEmployeeApplicationInviteDto
		{
			Email = $"work-assignment.{Guid.NewGuid():N}@example.com", PositionAppliedFor = "Peon",
			WorkAssignment = new()
			{
				Mode = EmployeeWorkAssignmentModes.SpecificOrganizations, AcademicYearId = seeded.AcademicYearId,
				OrganizationIds = [seeded.CbseEnglishSecondaryStageId, seeded.GsebGujaratiPrimaryStageId]
			}
		}, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var invite = (await response.Content.ReadFromJsonAsync<EmployeeApplicationInviteDto>(JsonOptions))!;
		invite.Application.WorkAssignment!.OrganizationLabels.ShouldBe(["CBSE - English - Secondary", "GSEB - Gujarati - Primary"]);
		var publicResponse = await CreateAnonymousClient().GetFromJsonAsync<PublicEmployeeApplicationDto>(
			$"/api/public/employee-applications/{invite.Token}", JsonOptions);
		publicResponse!.WorkAssignment!.OrganizationIds.ShouldBe([seeded.CbseEnglishSecondaryStageId, seeded.GsebGujaratiPrimaryStageId]);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task WorkAssignment_Invite_Rejects_Other_Year_And_Actor_Scope_Ids(bool restrictedActor)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var seeded = await SeedAcademicOrganizationsAsync();
		using var client = restrictedActor ? CreateClientAs(Guid.NewGuid()) : Factory.CreateClient();
		var response = await client.PostAsJsonAsync("/api/employee-applications", new CreateEmployeeApplicationInviteDto
		{
			Email = $"invalid-work-assignment.{Guid.NewGuid():N}@example.com",
			WorkAssignment = new()
			{
				Mode = EmployeeWorkAssignmentModes.SpecificOrganizations, AcademicYearId = seeded.AcademicYearId,
				OrganizationIds = [restrictedActor ? seeded.GsebGujaratiPrimaryStageId : seeded.OtherYearStageId]
			}
		}, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
	}
}
