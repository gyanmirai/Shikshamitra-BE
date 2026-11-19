using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.Records;

public sealed partial class StudentRecordsApplicationPostgres_Tests
{
	[StudentRecordsApplicationPostgresFact]
	public async Task Onboarding_worklist_postgres_filters_scope_counts_and_stable_sorts_before_paging()
	{
		Client.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		var fixture = await SeedOnboardingWorklistReadModelsAsync();
		var root = $"/api/student/onboarding?academicYearId={factory.SeededAcademicYearId}&search=WORKLIST-PG";
		var filter = root + $"&placementDivisionId={fixture.DivisionA}&studentNameFilter=ALICE%20PATEL&studentNameOperator=equals" +
			"&ownerNameFilter=mira&ownerNameOperator=startsWith&rollNumberOperator=between&rollNumberMin=1&rollNumberMax=3&sorting=StudentName%20asc&maxResultCount=1";
		var first = await GetOnboardingWorklistPageAsync(Client, filter);
		var next = await GetOnboardingWorklistPageAsync(Client, filter + "&skipCount=1");
		first.TotalCount.ShouldBe(2); next.TotalCount.ShouldBe(2);
		var expected = fixture.Cases.Take(2).Order().ToArray();
		first.Items.Single().Id.ShouldBe(expected[0]); next.Items.Single().Id.ShouldBe(expected[1]);
		(await GetOnboardingWorklistPageAsync(Client, root + "&rollNumberOperator=greaterThan&rollNumberFilter=3&studentNameFilter=patel&studentNameOperator=endsWith"))
			.Items.Single().RollNumber.ShouldBe(5);
		(await GetOnboardingWorklistPageAsync(Client, root + "&ownerNameFilter=not%20assigned&ownerNameOperator=equals")).TotalCount.ShouldBe(1);
		foreach (var sort in new[] { "Placement asc", "DivisionName desc", "AssignedToUserName asc", "OwnerName desc", "RollNumber desc", "Status asc" })
			(await GetOnboardingWorklistPageAsync(Client, root + "&sorting=" + Uri.EscapeDataString(sort))).TotalCount.ShouldBe(3);
		using var scoped = CreateClientAs(fixture.Staff); scoped.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		(await GetOnboardingWorklistPageAsync(scoped, root + "&studentNameFilter=patel&sorting=StudentName%20desc")).TotalCount.ShouldBe(2);
		(await GetOnboardingWorklistPageAsync(scoped, root + $"&placementDivisionId={fixture.DivisionB}")).TotalCount.ShouldBe(0);
		using var invalid = await Client.GetAsync(root + "&sorting=StudentName%20desc%2C%20Id%20asc");
		invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await invalid.Content.ReadAsStringAsync());
	}

	private async Task<PagedResultDto<StudentOnboardingCaseDto>> GetOnboardingWorklistPageAsync(System.Net.Http.HttpClient client, string url)
	{
		using var response = await client.GetAsync(url);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<Result<PagedResultDto<StudentOnboardingCaseDto>>>(JsonOptions))!.Data!;
	}

	// Only seed read models: the gate exercises real PostgreSQL query translation and HTTP scope, not activation writes.
	private Task<OnboardingWorklistPostgresFixture> SeedOnboardingWorklistReadModelsAsync() => InTenantAsync(async services =>
	{
		var cycle = new AdmissionCycle(Guid.NewGuid(), TenantId, "Worklist provider test", "WORKLIST-PG", factory.SeededAcademicYearId,
			DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(30));
		await services.GetRequiredService<IRepository<AdmissionCycle, Guid>>().InsertAsync(cycle, true);
		var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), TenantId, "Worklist provider test", "WORKLIST-PG");
		await services.GetRequiredService<IRepository<AdmissionWorkflowDefinition, Guid>>().InsertAsync(workflow, true);
		var form = new AdmissionFormDefinition(Guid.NewGuid(), TenantId, "Worklist provider test", "WORKLIST-PG", AdmissionFormLayoutType.SinglePage);
		await services.GetRequiredService<IRepository<AdmissionFormDefinition, Guid>>().InsertAsync(form, true);
		var offering = new AdmissionOffering(Guid.NewGuid(), TenantId, cycle.Id, null, factory.SeededBoardId, factory.SeededMediumId,
			factory.SeededMediumStageId, factory.SeededStandardId, "WORKLIST-PG", true, null, null);
		await services.GetRequiredService<IRepository<AdmissionOffering, Guid>>().InsertAsync(offering, true);
		var manager = services.GetRequiredService<DivisionManager>(); var divisions = services.GetRequiredService<IRepository<Division, Guid>>();
		var divisionA = await divisions.InsertAsync(await manager.CreateAsync(factory.SeededStandardId, "Worklist PG A", 30), true);
		var divisionB = await divisions.InsertAsync(await manager.CreateAsync(factory.SeededStandardId, "Worklist PG B", 30), true);
		var users = services.GetRequiredService<IRepository<IdentityUser, Guid>>();
		var owner = new IdentityUser(Guid.NewGuid(), "worklist-pg-owner", "worklist-pg-owner@example.test", TenantId) { Name = "Mira", Surname = "Staff" };
		await users.InsertAsync(owner, true);
		var staff = new IdentityUser(Guid.NewGuid(), "worklist-pg-staff", "worklist-pg-staff@example.test", TenantId); await users.InsertAsync(staff, true);
		await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TenantId,
			userId: staff.Id, academicYearId: factory.SeededAcademicYearId, educationBoardId: factory.SeededBoardId,
			educationMediumId: factory.SeededMediumId, educationMediumStageId: factory.SeededMediumStageId,
			standardId: factory.SeededStandardId, divisionId: divisionA.Id), true);
		var cases = new Guid[3];
		for (var index = 0; index < cases.Length; index++)
		{
			var firstName = index == 2 ? "Bob" : "Alice";
			var family = new AdmissionFamily(Guid.NewGuid(), TenantId, $"WORKLIST-PG-{index}");
			await services.GetRequiredService<IRepository<AdmissionFamily, Guid>>().InsertAsync(family, true);
			var student = new Student(Guid.NewGuid(), TenantId, firstName, null, "Patel", factory.SeededAcademicYearId,
				factory.SeededBoardId, factory.SeededMediumId, factory.SeededMediumStageId, factory.SeededStandardId);
			await services.GetRequiredService<IStudentRepository>().InsertAsync(student, true);
			var applicant = new AdmissionApplicant(Guid.NewGuid(), TenantId, family.Id, firstName, null, "Patel", null, null, null, student.Id);
			await services.GetRequiredService<IRepository<AdmissionApplicant, Guid>>().InsertAsync(applicant, true);
			var application = new AdmissionApplication(Guid.NewGuid(), TenantId, $"WORKLIST-PG-{index}", applicant.Id, null,
				cycle.Id, offering.Id, workflow.Id, form.Id);
			await services.GetRequiredService<IRepository<AdmissionApplication, Guid>>().InsertAsync(application, true);
			var item = new StudentOnboardingCase(Guid.NewGuid(), TenantId, application.Id, applicant.Id, student.Id, offering.Id,
				factory.SeededAcademicYearId, factory.SeededBoardId, factory.SeededMediumId, factory.SeededMediumStageId,
				factory.SeededStandardId, $"worklist-pg-{index}", null, DateTime.UtcNow.AddDays(7));
			var casesRepository = services.GetRequiredService<IRepository<StudentOnboardingCase, Guid>>();
			await casesRepository.InsertAsync(item, true);
			var reservation = new StudentSeatReservation(Guid.NewGuid(), TenantId, item.Id, factory.SeededAcademicYearId,
				factory.SeededStandardId, index == 2 ? divisionB.Id : divisionA.Id, index == 1 ? 2 : 1, DateTime.UtcNow, DateTime.UtcNow.AddHours(1));
			await services.GetRequiredService<IRepository<StudentSeatReservation, Guid>>().InsertAsync(reservation, true);
			item.RecordPlacement(reservation.DivisionId, reservation.Id, DateTime.UtcNow);
			item.RecordIdentifier(null, index == 0 ? 1 : index == 1 ? 3 : 5, DateTime.UtcNow);
			if (index != 2) item.AssignOwner(owner.Id);
			await SeedOnboardingWorklistPostgresRequirementsAsync(services, item, application, student);
			await casesRepository.UpdateAsync(item, true); cases[index] = item.Id;
		}
		return new OnboardingWorklistPostgresFixture(divisionA.Id, divisionB.Id, staff.Id, cases);
	});

	private static async Task SeedOnboardingWorklistPostgresRequirementsAsync(IServiceProvider services,
		StudentOnboardingCase item, AdmissionApplication application, Student student)
	{
		var snapshots = services.GetRequiredService<OnboardingRequirementSnapshotService>();
		await snapshots.CaptureAsync(item);
		var documentsReady = await snapshots.DocumentsReadyAsync(item, application);
		var steps = services.GetRequiredService<IRepository<StudentOnboardingStep, Guid>>();
		// Mirror the canonical default-step initialization without waivers or fabricated readiness.
		for (var index = 0; index < StudentOnboardingStepKeys.Defaults.Length; index++)
		{
			var definition = StudentOnboardingStepKeys.Defaults[index];
			var required = definition.Key is not (StudentOnboardingStepKeys.Family or StudentOnboardingStepKeys.Communication or StudentOnboardingStepKeys.MedicalAndPreviousSchool);
			var step = new StudentOnboardingStep(Guid.NewGuid(), item.TenantId, item.Id, definition.Key, definition.Label,
				index + 1, required, required ? StudentOnboardingStepStatus.Pending : StudentOnboardingStepStatus.NotRequired);
			if (definition.Key == StudentOnboardingStepKeys.Identity)
			{
				if (AutomaticOnboardingReadinessRules.HasCompleteIdentity(student)) step.Complete(null, DateTime.UtcNow, "Core identity verified in the fixture.");
				else step.Block("Complete the Student name, date of birth, and gender.");
			}
			if (definition.Key == StudentOnboardingStepKeys.Documents && documentsReady)
				step.Complete(null, DateTime.UtcNow, "Applicable document requirements evaluated by the snapshot owner.");
			if (definition.Key == StudentOnboardingStepKeys.PlacementAndSeat && item.SeatReservationId.HasValue)
				step.Complete(null, DateTime.UtcNow, "Fixture placement has a live capacity reservation.");
			await steps.InsertAsync(step, true);
		}
	}

	private sealed record OnboardingWorklistPostgresFixture(Guid DivisionA, Guid DivisionB, Guid Staff, Guid[] Cases);
}
