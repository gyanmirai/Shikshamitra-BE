using System;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

public sealed partial class StudentRecordsApplicationPostgres_Tests
{
	[StudentRecordsApplicationPostgresFact]
	public async Task Record_postgres_return_cancellation_and_activation_have_one_winner_and_preserve_history()
	{
		Client.Timeout = TimeSpan.FromMinutes(4); Client.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		var fixture = await SeedHistoricalStudentsAndTargetAsync();
		await InTenantAsync(async services =>
		{
			foreach (var id in fixture.Students)
				await services.GetRequiredService<IRepository<StudentAdditionalDetail, Guid>>().InsertAsync(new(id, TenantId,
					new DateTimeOffset(2016, 1, 12, 0, 0, 0, TimeSpan.Zero), GenderEnum.Female, null, "PG-OWNER-" + id.ToString("N"), null, null), true);
			return true;
		});
		using var reviewer = CreateClientAs(fixture.Reviewer); reviewer.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		foreach (var id in fixture.Students)
		{
			var legacy = await PostResultAsync<StudentLegacyOverrideDto>(Client, $"/api/students/records/directory/{id}/legacy-overrides",
				new RequestStudentLegacyOverrideDto { Reason = "Synthetic historical evidence for owner race" });
			await PostResultAsync<StudentLegacyOverrideDto>(reviewer, $"/api/students/records/directory/legacy-overrides/{legacy.Id}/approve",
				new DecideStudentLegacyOverrideDto { ConcurrencyStamp = legacy.ConcurrencyStamp, Remarks = "Independent historical verification" });
		}
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Status"].Mode = "Direct";
		using var save = await Client.PutAsJsonAsync("/api/students/records/settings/draft", new SaveStudentRecordsSettingsDto { Policy = policy }, JsonOptions);
		save.StatusCode.ShouldBe(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());
		var draft = (await save.Content.ReadFromJsonAsync<Result<StudentRecordsSettingsDto>>(JsonOptions))!.Data!;
		await PostResultAsync<StudentRecordsSettingsDto>(Client, "/api/students/records/settings/publish", new PublishStudentRecordsSettingsDto { ConcurrencyStamp = draft.ConcurrencyStamp! });
		var offering = await SeedAdmissionReturnConcurrencyOfferingAsync(fixture, AdmissionEntryMode.DirectRegistration);
		await PostResultAsync<StudentOnboardingPolicyDto>(Client, "/api/student/onboarding/settings/policies", new UpsertStudentOnboardingPolicyDto
		{ AcademicYearId = fixture.SourceYear, Name = "Owner race", RequireDocuments = false, RequireFamilyDetails = false,
			RequireCommunicationDetails = false, RequireStudentAccount = false, RequireGuardianAccount = false });
		for (var index = 0; index < fixture.Students.Length; index++)
		{
			var id = fixture.Students[index]; var today = AdmissionCycleWindowEvaluator.GetInstitutionDate(DateTime.UtcNow);
			var exit = new StudentRecordTransitionBatchInput { AcademicYearId = fixture.SourceYear, IdempotencyKey = "owner-exit-" + id.ToString("N"),
				Items = [new() { StudentId = id, Outcome = "Withdraw", EffectiveDate = today, Reason = "Synthetic reviewed exit before owner race" }] };
			var preview = (await PostResultAsync<StudentRecordTransitionPreviewDto>(Client, Transitions + "/preview", exit)).Items.Single();
			exit.Items[0].StudentConcurrencyStamp = preview.StudentConcurrencyStamp; exit.Items[0].EnrollmentConcurrencyStamp = preview.EnrollmentConcurrencyStamp;
			(await PostResultAsync<StudentRecordTransitionBatchResultDto>(Client, Transitions + "/batches", exit)).Items.Single().Status.ShouldBe("Applied");
			var request = new StudentRecordReturnInput { StudentId = id, Route = "FreshApplication", TargetAcademicYearId = fixture.SourceYear,
				AdmissionOfferingId = offering.Id, EffectiveDate = today, Reason = "Reviewed return for owner race", IdempotencyKey = "owner-return-" + id.ToString("N"),
				Registration = new() { GuardianFirstName = "Owner", GuardianLastName = "Guardian", GuardianMobile = "987654321" + index } };
			var returned = await PostResultAsync<StudentRecordReturnDto>(Client, Transitions + "/returns", request);
			var item = await PrepareAdmissionReturnRaceCaseAsync(returned.AdmissionApplicationId!.Value, id, index + 17);
			var before = await ReadAdmissionReturnSourceRowsAsync(id);
			factory.Barrier.Arm("UPDATE \"AppStudentOnboardingCases\"", isolation: null);
			var activation = Client.PostAsJsonAsync($"/api/student/onboarding/{item.Id}/activate", new ActivateStudentOnboardingDto
			{ ConcurrencyStamp = item.ConcurrencyStamp, IdempotencyKey = "owner-activate-" + item.Id.ToString("N") }, JsonOptions);
			var cancellation = Client.PostAsJsonAsync($"/api/student/onboarding/{item.Id}/cancel", new CancelStudentOnboardingDto
			{ ConcurrencyStamp = item.ConcurrencyStamp, Reason = "Family cancels while activation is being reviewed" }, JsonOptions);
			try { await factory.Barrier.Reached.WaitAsync(TimeSpan.FromSeconds(30)); }
			finally { factory.Barrier.Release(); }
			var responses = await Task.WhenAll(activation, cancellation).WaitAsync(TimeSpan.FromSeconds(60));
			try
			{
				factory.Barrier.Connections.Count.ShouldBe(2);
				factory.Barrier.ConnectionIsolations.Values.ShouldContain(IsolationLevel.Serializable);
				factory.Barrier.ConnectionIsolations.Values.ShouldContain(IsolationLevel.ReadCommitted);
				responses.Count(x => x.StatusCode == HttpStatusCode.OK).ShouldBe(1, string.Join("; ", await Task.WhenAll(responses.Select(x => x.Content.ReadAsStringAsync()))));
				var loser = responses.Single(x => x.StatusCode != HttpStatusCode.OK);
				output.WriteLine($"Activation {(int)responses[0].StatusCode}; cancellation {(int)responses[1].StatusCode}; provider conflicts {string.Join(",", factory.Barrier.ProviderFailures.Keys)}; losing response {await loser.Content.ReadAsStringAsync()}");
				loser.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Conflict);
				var current = await GetAdmissionReturnRaceResultAsync<StudentOnboardingCaseDto>($"/api/student/onboarding/{item.Id}");
				var cancelled = responses[1].StatusCode == HttpStatusCode.OK;
				current.Status.ShouldBe(cancelled ? StudentOnboardingStatus.Cancelled : StudentOnboardingStatus.Provisioning);
				(await PostResultAsync<StudentRecordReturnDto>(Client, Transitions + "/returns", request)).Status.ShouldBe(cancelled ? "Cancelled" : "AwaitingAdmission");
				await InTenantAsync(async services =>
				{
					var enrollment = await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().GetAsync(x => x.StudentId == id);
					enrollment.IsActive.ShouldBe(!cancelled); enrollment.OnboardingCaseId.ShouldBeNull();
					(await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().CountAsync(x => x.StudentId == id)).ShouldBe(1);
					(await services.GetRequiredService<IStudentRepository>().GetAsync(id)).GrNumber.ShouldBe("PG-RECORDS-" + index);
					(await services.GetRequiredService<IRepository<StudentRecordTransitionEvent, Guid>>().CountAsync(x => x.TransitionId == returned.TransitionId && x.Action == "OnboardingCancelled")).ShouldBe(cancelled ? 1 : 0);
					return true;
				});
				var after = await ReadAdmissionReturnSourceRowsAsync(id); after.Detail.ShouldBe(before.Detail);
				if (cancelled) after.Enrollment.ShouldBe(before.Enrollment);
				output.WriteLine($"Owner race {index}: {(cancelled ? "cancellation" : "activation")} won; one enrollment, permanent GR and history preserved; loser returned {(int)loser.StatusCode}.");
			}
			finally { foreach (var response in responses) response.Dispose(); }
		}
	}

	private async Task<StudentOnboardingCaseDto> PrepareAdmissionReturnRaceCaseAsync(Guid applicationId, Guid studentId, int roll)
	{
		var root = $"/api/student/enrollment/admissions/applications/{applicationId}";
		var submitted = await PostResultAsync<AdmissionApplicationSubmitResultDto>(Client, root + "/submit", new { }); submitted.ValidationErrors.ShouldBeEmpty();
		for (var index = 0; index < 3; index++)
		{
			var runtime = await GetAdmissionReturnRaceResultAsync<AdmissionApplicationWorkflowDto>(root + "/workflow");
			if (runtime.Status == AdmissionWorkflowInstanceStatus.Completed) break;
			var application = await GetAdmissionReturnRaceResultAsync<AdmissionApplicationDto>(root);
			var stageId = runtime.CurrentStageInstanceId.ShouldNotBeNull();
			await PostResultAsync<AdmissionApplicationWorkflowDto>(Client, root + "/workflow/advance", new AdvanceAdmissionWorkflowDto
			{ ExpectedCurrentStageInstanceId = stageId, ConcurrencyStamp = application.ConcurrencyStamp, IdempotencyKey = "owner-stage-" + stageId });
		}
		var ready = await GetAdmissionReturnRaceResultAsync<AdmissionApplicationDto>(root);
		ready = await PostResultAsync<AdmissionApplicationDto>(Client, root + "/decision/approve", new AdmissionApplicationDecisionDto { ConcurrencyStamp = ready.ConcurrencyStamp, Reason = "Owner race admission reviewed" });
		ready = await PostResultAsync<AdmissionApplicationDto>(Client, root + "/offer", new AdmissionApplicationDecisionDto { ConcurrencyStamp = ready.ConcurrencyStamp, OfferExpiresAt = DateTime.UtcNow.AddDays(30) });
		ready = await PostResultAsync<AdmissionApplicationDto>(Client, root + "/offer/accept", new AdmissionApplicationDecisionDto { ConcurrencyStamp = ready.ConcurrencyStamp });
		var finalized = await PostResultAsync<AdmissionFinalizationResultDto>(Client, root + "/finalize", new FinalizeAdmissionApplicationDto
		{ ConcurrencyStamp = ready.ConcurrencyStamp, IdempotencyKey = "owner-finalize-" + applicationId.ToString("N"), ConfirmGuardianContactVerified = true });
		finalized.StudentId.ShouldBe(studentId);
		var item = await GetAdmissionReturnRaceResultAsync<StudentOnboardingCaseDto>($"/api/student/onboarding/{finalized.OnboardingCaseId}");
		var division = await InTenantAsync(async services => (await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().GetAsync(x => x.StudentId == studentId)).DivisionId!.Value);
		item = await PostResultAsync<StudentOnboardingCaseDto>(Client, $"/api/student/onboarding/{item.Id}/placement", new SetStudentOnboardingPlacementDto { ConcurrencyStamp = item.ConcurrencyStamp, DivisionId = division });
		item = await PostResultAsync<StudentOnboardingCaseDto>(Client, $"/api/student/onboarding/{item.Id}/identifiers", new AllocateStudentOnboardingIdentifierDto { ConcurrencyStamp = item.ConcurrencyStamp, NumberSeriesId = Guid.NewGuid(), RollNumber = roll });
		return await PostResultAsync<StudentOnboardingCaseDto>(Client, $"/api/student/onboarding/{item.Id}/readiness/refresh?concurrencyStamp={Uri.EscapeDataString(item.ConcurrencyStamp!)}", new { });
	}

	private async Task<T> GetAdmissionReturnRaceResultAsync<T>(string url)
	{
		using var response = await Client.GetAsync(url); response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<Result<T>>(JsonOptions))!.Data!;
	}
}
