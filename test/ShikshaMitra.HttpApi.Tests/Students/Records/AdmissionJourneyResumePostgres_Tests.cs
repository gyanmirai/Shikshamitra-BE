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
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

public sealed partial class StudentRecordsApplicationPostgres_Tests
{
	[StudentRecordsApplicationPostgresFact]
	public async Task Admission_resume_postgres_same_closure_race_has_one_winner_and_one_immutable_event()
	{
		const string root = "/api/student/enrollment/admissions/applications";
		Client.Timeout = TimeSpan.FromMinutes(3);
		Client.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		var template = await SeedAdmissionReturnConcurrencyOfferingAsync(
			new Fixture(factory.SeededAcademicYearId, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, []), AdmissionEntryMode.DirectRegistration);
		// The shared Records fixture deliberately seeds a historical year. Resume must use a current, independently scoped intake.
		var offering = await InTenantAsync(async services =>
		{
			var today = DateTime.UtcNow.Date;
			var year = await services.GetRequiredService<AcademicYearManager>().CreateAsync("Resume PG current " + Guid.NewGuid().ToString("N"), today.AddMonths(-1), today.AddMonths(11));
			await services.GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
			var board = await services.GetRequiredService<EducationBoardManager>().CreateAsync(year.Id, EducationBoardType.CBSE);
			await services.GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, true);
			var medium = await services.GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "Resume PG English");
			await services.GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, true);
			var stage = await services.GetRequiredService<EducationMediumStageManager>().CreateAsync(medium.Id, "Resume PG Primary");
			await services.GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
			var standard = await services.GetRequiredService<StandardManager>().CreateAsync(stage.Id, "Resume PG Class", 1, null);
			await services.GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(standard, true);
			var cycle = new AdmissionCycle(Guid.NewGuid(), TenantId, "Resume PG current", "RESUME-PG", year.Id, today.AddDays(-1), today.AddDays(30));
			cycle.Publish(Guid.Parse(TestAuthHandler.TestUserId), DateTime.UtcNow);
			await services.GetRequiredService<IRepository<AdmissionCycle, Guid>>().InsertAsync(cycle, true);
			var current = new AdmissionOffering(Guid.NewGuid(), TenantId, cycle.Id, null, board.Id, medium.Id, stage.Id, standard.Id,
				"RESUME-PG-CURRENT", true, null, null, entryMode: AdmissionEntryMode.DirectRegistration);
			current.ConfigureReferences(template.WorkflowDefinitionId, null, template.ApplicationFormDefinitionId);
			await services.GetRequiredService<IRepository<AdmissionOffering, Guid>>().InsertAsync(current, true);
			return current;
		});
		var application = await InTenantAsync(async services =>
		{
			await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId,
				userId: Guid.Parse(TestAuthHandler.TestUserId), description: "Synthetic admission resume provider test"), true);
			var family = new AdmissionFamily(Guid.NewGuid(), TenantId, "PG-RESUME-FAMILY");
			await services.GetRequiredService<IRepository<AdmissionFamily, Guid>>().InsertAsync(family, true);
			var applicant = new AdmissionApplicant(Guid.NewGuid(), TenantId, family.Id, "Resume", null, "Provider", null, null, null, Guid.NewGuid());
			await services.GetRequiredService<IRepository<AdmissionApplicant, Guid>>().InsertAsync(applicant, true);
			var entity = new AdmissionApplication(Guid.NewGuid(), TenantId, "PG-RESUME-0001", applicant.Id, null,
				offering.AdmissionCycleId, offering.Id, offering.WorkflowDefinitionId!.Value, offering.ApplicationFormDefinitionId!.Value);
			var workflow = new AdmissionApplicationWorkflowInstance(Guid.NewGuid(), TenantId, entity.Id, entity.WorkflowDefinitionId);
			var definitions = await services.GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>().GetListAsync(x => x.WorkflowDefinitionId == entity.WorkflowDefinitionId);
			var runtime = definitions.Select(x => new AdmissionApplicationStageInstance(Guid.NewGuid(), TenantId, workflow.Id, x.Id,
				x.Code, x.Name, x.StageType, x.DisplayOrder, x.IsEntryStage)).ToArray();
			entity.SetCurrentStage(runtime.Single(x => x.Status == AdmissionRuntimeStageStatus.Active).Id);
			await services.GetRequiredService<IRepository<AdmissionApplication, Guid>>().InsertAsync(entity, true);
			await services.GetRequiredService<IRepository<AdmissionApplicationWorkflowInstance, Guid>>().InsertAsync(workflow, true);
			await services.GetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>().InsertManyAsync(runtime, true);
			await services.GetRequiredService<IRepository<AdmissionFormInstance, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, entity.Id, entity.FormDefinitionId, 1), true);
			return entity;
		});
		var url = $"{root}/{application.Id}";
		await PostResultAsync<AdmissionApplicationDto>(Client, url + "/end-journey", new EndAdmissionJourneyDto { Reason = "Provider pause", ConcurrencyStamp = application.ConcurrencyStamp });
		var preview = (await Client.GetFromJsonAsync<Result<AdmissionJourneyResumePreviewDto>>(url + "/resume-preview", JsonOptions))!.Data!;
		preview.Blockers.ShouldBeEmpty();
		var command = new ResumeAdmissionJourneyDto { Reason = "Provider return", ConcurrencyStamp = preview.ConcurrencyStamp,
			ClosureHistoryId = preview.ClosureHistoryId!.Value, IdempotencyKey = "postgres-resume-same-closure" };
		using (var anonymous = CreateAnonymousClient())
			(await anonymous.GetAsync(url + "/resume-preview")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		using (var reader = CreateClientAs(Guid.Parse(TestAuthHandler.TestUserId)))
		{
			reader.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
			reader.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
				ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View);
			(await reader.GetAsync(url + "/resume-preview")).StatusCode.ShouldBe(HttpStatusCode.OK);
			(await reader.PostAsJsonAsync(url + "/resume-journey", command, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		}
		factory.Barrier.Arm("UPDATE \"AppAdmissionApplications\"");
		var first = Client.PostAsJsonAsync(url + "/resume-journey", command, JsonOptions);
		var second = Client.PostAsJsonAsync(url + "/resume-journey", command, JsonOptions);
		try
		{
			await factory.Barrier.Reached.WaitAsync(TimeSpan.FromSeconds(30));
			var resolved = await PostResultAsync<AdmissionApplicationDto>(Client,
				$"{root}/start?applicantId={application.ApplicantId}&offeringId={offering.Id}", new { });
			resolved.Id.ShouldBe(application.Id); resolved.ApplicationNumber.ShouldBe(application.ApplicationNumber);
			resolved.Status.ShouldBe(AdmissionApplicationStatus.Withdrawn); // No implicit resume or new sequence allocation.
		}
		finally { factory.Barrier.Release(); }
		var responses = await Task.WhenAll(first, second);
		responses.Count(x => x.StatusCode == HttpStatusCode.OK).ShouldBe(1, string.Join("\n", await Task.WhenAll(responses.Select(x => x.Content.ReadAsStringAsync()))));
		responses.Count(x => x.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
		var replay = await PostResultAsync<AdmissionApplicationDto>(Client, url + "/resume-journey", command);
		replay.ApplicationNumber.ShouldBe(application.ApplicationNumber); replay.CurrentStageInstanceId.ShouldBe(application.CurrentStageInstanceId);
		await InTenantAsync(async services =>
		{
			(await services.GetRequiredService<IRepository<AdmissionApplicationDecisionHistory, Guid>>().CountAsync(x => x.ApplicationId == application.Id && x.Action == "journey-resumed")).ShouldBe(1);
			(await services.GetRequiredService<IRepository<AdmissionApplication, Guid>>().CountAsync(x => x.ApplicantId == application.ApplicantId && x.AdmissionOfferingId == offering.Id)).ShouldBe(1);
			return true;
		});
		foreach (var response in responses) response.Dispose();
	}
}
