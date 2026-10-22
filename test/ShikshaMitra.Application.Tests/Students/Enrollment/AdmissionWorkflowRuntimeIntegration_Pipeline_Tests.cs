using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Pipeline_progress_should_use_each_application_runtime_without_mutating_or_mixing_workflows() =>
		RunCertificationScenarioAsync(async () =>
		{
			var shortOffering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
			var longOffering = await CreateAvailableOfferingAsync((await CreateAndPublishRealisticWorkflowAsync()).Id);
			var first = await StartFixtureApplicationAsync((await CreateApplicantAsync("Short journey")).Id, shortOffering.Id);
			var second = await StartFixtureApplicationAsync((await CreateApplicantAsync("Long journey")).Id, longOffering.Id);
			var firstWorkflow = await _applicationAppService.GetWorkflowAsync(first.Id);
			var secondWorkflow = await _applicationAppService.GetWorkflowAsync(second.Id);
			firstWorkflow.Stages.Count.ShouldNotBe(secondWorkflow.Stages.Count);
			var page = await _applicationAppService.GetPipelineAsync(new() { MyCases = false, MaxResultCount = 100 });
			foreach (var pair in new[] { (first.Id, Workflow: firstWorkflow), (second.Id, Workflow: secondWorkflow) })
			{
				var row = page.Items.Single(item => item.Id == pair.Id);
				row.StageProgress.Select(item => item.Id).ShouldBe(pair.Workflow.Stages.Select(item => item.Id));
				row.StageProgress.Select(item => item.Status).ShouldBe(pair.Workflow.Stages.Select(item => item.Status));
				row.StageProgress.Select(item => item.Name).ShouldBe(pair.Workflow.Stages.Select(item => item.Name));
				row.StageProgress.Single(item => item.Id == row.CurrentStageInstanceId).Status.ShouldBe(AdmissionRuntimeStageStatus.Active);
				var unchanged = await _applicationAppService.GetWorkflowAsync(pair.Id);
				unchanged.CurrentStageInstanceId.ShouldBe(pair.Workflow.CurrentStageInstanceId);
				unchanged.Stages.Select(item => item.Status).ShouldBe(pair.Workflow.Stages.Select(item => item.Status));
			}
			var scoped = await _applicationAppService.GetPipelineAsync(new() { MyCases = false, AdmissionOfferingId = shortOffering.Id, MaxResultCount = 1 });
			scoped.Items.Single().StageProgress.Select(item => item.Id).ShouldBe(firstWorkflow.Stages.Select(item => item.Id));
		});

	[Fact]
	public Task Pipeline_should_use_configured_fallback_manager_default_without_role_name_rules() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
			await WithUnitOfWorkAsync(async () =>
			{
				var profile = new AdmissionIntakeProfile(Guid.NewGuid(), TestTenantId, AdmissionIntakeProfileKind.CounsellorAssignment, "PIPELINE-MANAGER", "Pipeline manager", JsonSerializer.Serialize(new AdmissionCounsellorAssignmentRules { CounsellorUserIds = [TestUserId], FallbackManagerUserId = TestUserId }));
				await GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<AdmissionIntakeProfile, Guid>>().InsertAsync(profile, true);
				var entity = await _offeringRepository.GetAsync(offering.Id);
				typeof(AdmissionOffering).GetProperty(nameof(AdmissionOffering.CounsellorAssignmentProfileId))!.SetValue(entity, profile.Id);
				await _offeringRepository.UpdateAsync(entity, true);
			});
			(await _applicationAppService.GetPipelineOptionsAsync(new())).DefaultMyCases.ShouldBeFalse();
		});

	[Fact]
	public Task Pipeline_should_keep_missing_source_visible_and_exclude_unlinked_historical_applications() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
			var application = await StartFixtureApplicationAsync((await CreateApplicantAsync("Legacy source")).Id, offering.Id);
			var historicalApplicant = await CreateApplicantAsync("Historical direct");
			var inquiryId = (await _applicationAppService.GetAsync(application.Id)).InquiryId!.Value;
			await WithUnitOfWorkAsync(async () =>
			{
				await GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<AdmissionInquiry, Guid>>().DeleteAsync(inquiryId, true);
				var old = new AdmissionApplication(Guid.NewGuid(), TestTenantId, "OLD-DIRECT", historicalApplicant.Id, null, offering.AdmissionCycleId, offering.Id, offering.WorkflowDefinitionId!.Value, offering.ApplicationFormDefinitionId!.Value);
				await _applicationRepository.InsertAsync(old, true);
			});
			var rows = await _applicationAppService.GetPipelineAsync(new() { MyCases = false, AdmissionOfferingId = offering.Id });
			rows.TotalCount.ShouldBe(1);
			rows.Items.Single().Id.ShouldBe(application.Id);
			rows.Items.Single().SourceInquiryAvailable.ShouldBeFalse();
			rows.Items.Single().InquiryNumber.ShouldBeNull();
			var missingSource = await Should.ThrowAsync<BusinessException>(() => _applicationAppService.GetFollowUpsAsync(application.Id));
			missingSource.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidState);
			missingSource.Data["Reason"].ShouldBe("SourceInquiryUnavailable");
		});

	[Fact]
	public Task Pipeline_should_project_current_application_owner_stage_and_complete_filter_choices() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
			var first = await StartFixtureApplicationAsync((await CreateApplicantAsync("Aarav")).Id, offering.Id);
			var second = await StartFixtureApplicationAsync((await CreateApplicantAsync("Diya")).Id, offering.Id);
			var secondOwner = await CreateReviewUserAsync();
			await WithUnitOfWorkAsync(async () =>
			{
				var a = await _applicationRepository.GetAsync(first.Id);
				a.AssignCounsellor(TestUserId);
				await _applicationRepository.UpdateAsync(a, true);
				var b = await _applicationRepository.GetAsync(second.Id);
				b.AssignCounsellor(secondOwner);
				await _applicationRepository.UpdateAsync(b, true);
				var stageRepository = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<AdmissionApplicationStageInstance, Guid>>();
				var stage = await stageRepository.GetAsync(b.CurrentStageInstanceId!.Value);
				typeof(AdmissionApplicationStageInstance).GetProperty(nameof(AdmissionApplicationStageInstance.Name))!.SetValue(stage, "Inquiry version two");
				await stageRepository.UpdateAsync(stage, true);
			});
			var page = await _applicationAppService.GetPipelineAsync(new() { MyCases = false, AdmissionOfferingId = offering.Id, MaxResultCount = 1, Sorting = "applicantName asc" });
			page.TotalCount.ShouldBe(2);
			page.Items.Single().Id.ShouldBe(first.Id);
			page.Items.Single().ApplicantName.ShouldContain("Aarav");
			page.Items.Single().AssignedCounsellorId.ShouldBe(TestUserId);
			page.Items.Single().CurrentStageCode.ShouldBe("INQUIRY");
			page.Items.Single().EnteredAt.ShouldNotBeNull();
			page.Items.Single().EnteredAt!.Value.Kind.ShouldBe(DateTimeKind.Utc);
			page.Items.Single().CreationTime.Kind.ShouldBe(DateTimeKind.Utc);
			var workflow = await WithUnitOfWorkAsync(() => GetRequiredService<AdmissionApplicationWorkflowReadService>().GetAsync(first.Id));
			workflow.StartedAt.Kind.ShouldBe(DateTimeKind.Utc);
			Math.Abs((page.Items.Single().CreationTime - workflow.StartedAt).TotalMinutes).ShouldBeLessThan(1);
			workflow.Stages.Single(item => item.Id == page.Items.Single().CurrentStageInstanceId).EnteredAt.ShouldBe(page.Items.Single().EnteredAt);
			workflow.Stages.Single(item => item.Id == page.Items.Single().CurrentStageInstanceId).EnteredAt!.Value.Kind.ShouldBe(DateTimeKind.Utc);
			var storedStage = await WithUnitOfWorkAsync(() => GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<AdmissionApplicationStageInstance, Guid>>().GetAsync(page.Items.Single().CurrentStageInstanceId!.Value));
			storedStage.EnteredAt!.Value.Kind.ShouldBe(DateTimeKind.Utc);
			storedStage.EnteredAt.Value.Ticks.ShouldBe(page.Items.Single().EnteredAt!.Value.Ticks);
			page.Items.Single().NextRequiredAction.ShouldBe("Review and qualify the inquiry");
			page.Items.Single().DueAt.ShouldBeNull();
			var options = await _applicationAppService.GetPipelineOptionsAsync(new() { AdmissionOfferingId = offering.Id, MaxResultCount = 1, Search = "Aarav", AssignedCounsellorId = TestUserId });
			options.Counsellors.Select(item => item.Id).ShouldContain(secondOwner);
			options.Stages.ShouldContain(item => item.Code == "INQUIRY");
			options.Stages.Single(item => item.Code == "INQUIRY").Name.ShouldBe("INQUIRY");
			(await _applicationAppService.GetPipelineAsync(new() { MyCases = true, AdmissionOfferingId = offering.Id })).Items.Single().Id.ShouldBe(first.Id);
			(await _applicationAppService.GetPipelineAsync(new() { MyCases = false, AdmissionOfferingId = offering.Id, Sorting = "creationTime asc" })).Items.First().Id.ShouldBe(first.Id);
			(await _applicationAppService.GetPipelineAsync(new() { MyCases = false, AdmissionOfferingId = offering.Id, Search = "diya" })).Items.Single().Id.ShouldBe(second.Id);
			(await _applicationAppService.GetPipelineAsync(new() { MyCases = false, CurrentStageCode = "MISSING" })).TotalCount.ShouldBe(0);
			(await _applicationAppService.GetPipelineAsync(new() { MyCases = false, AcademicYearId = Guid.NewGuid() })).TotalCount.ShouldBe(0);
		});

	[Theory]
	[InlineData(AdmissionApplicationStatus.Withdrawn)]
	[InlineData(AdmissionApplicationStatus.Rejected)]
	[InlineData(AdmissionApplicationStatus.OfferDeclined)]
	[InlineData(AdmissionApplicationStatus.Enrolled)]
	[InlineData(AdmissionApplicationStatus.AdmissionFinalized)]
	public Task Pipeline_should_exclude_terminal_admissions(AdmissionApplicationStatus status) =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
			var application = await StartFixtureApplicationAsync((await CreateApplicantAsync("Closed applicant")).Id, offering.Id);
			await WithUnitOfWorkAsync(async () =>
			{
				var entity = await _applicationRepository.GetAsync(application.Id);
				typeof(AdmissionApplication).GetProperty(nameof(AdmissionApplication.Status))!.SetValue(entity, status);
				await _applicationRepository.UpdateAsync(entity, true);
			});
			(await _applicationAppService.GetPipelineAsync(new() { MyCases = false, AdmissionOfferingId = offering.Id })).TotalCount.ShouldBe(0);
			(await _applicationAppService.GetAsync(application.Id)).Status.ShouldBe(status);
		});

	[Fact]
	public Task Pipeline_and_application_followups_should_fail_closed_for_missing_academic_scope() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
			var application = await StartFixtureApplicationAsync((await CreateApplicantAsync("Protected applicant")).Id, offering.Id);
			using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString()), new Claim(AbpClaimTypes.UserName, "no-scope") }, "Test"))))
			{
				(await _applicationAppService.GetPipelineAsync(new() { MyCases = false })).TotalCount.ShouldBe(0);
				(await _applicationAppService.GetPipelineOptionsAsync(new())).Counsellors.ShouldBeEmpty();
				await Should.ThrowAsync<EntityNotFoundException>(() => _applicationAppService.GetFollowUpsAsync(application.Id));
			}
		});

	[Fact]
	public Task Pipeline_application_history_should_preserve_closed_case_reads_and_deny_unauthorized_scope() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
			var application = await StartFixtureApplicationAsync((await CreateApplicantAsync("History scope")).Id, offering.Id);
			var history = GetRequiredService<IAdmissionActivityAppService>();
			var activity = new AdmissionActivity(Guid.NewGuid(), TestTenantId, "PipelineHistoryTest", "Scoped application history", application.ApplicantId, application.Id);
			await WithUnitOfWorkAsync(async () => await GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<AdmissionActivity, Guid>>().InsertAsync(activity, true));
			var projected = (await history.GetForApplicationAsync(application.Id)).Single(item => item.Id == activity.Id);
			projected.OccurredAt.Kind.ShouldBe(DateTimeKind.Utc);
			projected.OccurredAt.ShouldBe(activity.OccurredAt);
			await WithUnitOfWorkAsync(async () =>
			{
				var entity = await _applicationRepository.GetAsync(application.Id);
				typeof(AdmissionApplication).GetProperty(nameof(AdmissionApplication.Status))!.SetValue(entity, AdmissionApplicationStatus.AdmissionFinalized);
				await _applicationRepository.UpdateAsync(entity, true);
			});
			(await history.GetForApplicationAsync(application.Id)).ShouldContain(item => item.Id == activity.Id);
			using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString()), new Claim(AbpClaimTypes.UserName, "history-no-scope") }, "Test"))))
				await Should.ThrowAsync<EntityNotFoundException>(() => history.GetForApplicationAsync(application.Id));
		});

	[Fact]
	public Task Pipeline_followups_should_preserve_handoff_dates_and_history_and_lock_terminal_commands() =>
		RunCertificationScenarioAsync(async () =>
		{
			await WithUnitOfWorkAsync(async () =>
			{
				var context = await GetRequiredService<Volo.Abp.EntityFrameworkCore.IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				var entity = context.Model.FindEntityType(typeof(AdmissionInquiryFollowUp))!;
				var sampleUtc = DateTime.UtcNow;
				foreach (var name in new[] { nameof(AdmissionInquiryFollowUp.ScheduledAt), nameof(AdmissionInquiryFollowUp.CompletedAt) })
				{
					var converter = entity.FindProperty(name)!.GetValueConverter();
					converter.ShouldNotBeNull();
					converter.ProviderClrType.ShouldBe(typeof(DateTimeOffset));
					var provider = (DateTimeOffset)converter.ConvertToProvider(sampleUtc)!;
					provider.Offset.ShouldBe(TimeSpan.Zero);
					provider.UtcDateTime.ShouldBe(sampleUtc);
					((DateTime)converter.ConvertFromProvider(provider)!).Kind.ShouldBe(DateTimeKind.Utc);
					converter.ConvertToProvider(null).ShouldBeNull();
				}
			});
			var offering = await CreateAvailableOfferingAsync((await CreateAndPublishManagedInquiryWorkflowAsync()).Id);
			var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
			var management = GetRequiredService<IAdmissionInquiryManagementAppService>();
			var inquiry = await inquiries.CreateAsync(ManagedInquiryInput(offering));
			var state = await management.GetAsync(inquiry.Id);
			var localTime = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(2).AddHours(10).AddMinutes(30), DateTimeKind.Unspecified);
			var scheduledAt = new DateTimeOffset(localTime, TimeSpan.FromHours(5.5));
			state = await management.ScheduleFollowUpAsync(inquiry.Id, new() { ConcurrencyStamp = state.ConcurrencyStamp, ScheduledAt = scheduledAt, Purpose = "Confirm visit" });
			inquiry = await inquiries.GetAsync(inquiry.Id);
			inquiry = await inquiries.UpdateAsync(inquiry.Id, InquiryStatusUpdate(inquiry, AdmissionInquiryStatus.Interested));
			var application = await _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, offering.Id, inquiry.Id);
			(await _applicationAppService.StartAsync(inquiry.ApplicantId.Value, offering.Id, inquiry.Id)).Id.ShouldBe(application.Id);
			await WithUnitOfWorkAsync(async () =>
			{
				var repository = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<AdmissionInquiryManagement, Guid>>();
				var current = (await repository.GetListAsync(item => item.InquiryId == inquiry.Id)).Single();
				typeof(AdmissionInquiryManagement).GetProperty(nameof(AdmissionInquiryManagement.FirstContactDueAt))!.SetValue(current, DateTime.UtcNow.AddMinutes(30));
				await repository.UpdateAsync(current, true);
			});
			state = await _applicationAppService.GetFollowUpsAsync(application.Id);
			state.IsLocked.ShouldBeTrue();
			state.IsOverdue.ShouldBeFalse();
			state.CanManageFollowUps.ShouldBeTrue();
			state.FollowUps.Single().ScheduledAt.ShouldBe(scheduledAt.UtcDateTime);
			state.FollowUps.Single().ScheduledAt.Kind.ShouldBe(DateTimeKind.Utc);
			JsonSerializer.Serialize(state.FollowUps.Single().ScheduledAt).ShouldBe(JsonSerializer.Serialize(scheduledAt.UtcDateTime));
			JsonSerializer.Serialize(state.FollowUps.Single().ScheduledAt).ShouldEndWith("Z\"");
			using (var apiJson = JsonDocument.Parse(GetRequiredService<Volo.Abp.Json.IJsonSerializer>().Serialize(state.FollowUps.Single())))
			{
				var apiDate = apiJson.RootElement.EnumerateObject().Single(property => string.Equals(property.Name, nameof(AdmissionInquiryFollowUpDto.ScheduledAt), StringComparison.OrdinalIgnoreCase)).Value.GetString()!;
				apiDate.ShouldEndWith("Z");
				DateTimeOffset.Parse(apiDate).ShouldBe(scheduledAt.ToUniversalTime());
			}
			new DateTimeOffset(state.FollowUps.Single().ScheduledAt).ToOffset(scheduledAt.Offset).DateTime.ShouldBe(localTime);
			await WithUnitOfWorkAsync(async () =>
			{
				var stored = await GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<AdmissionInquiryFollowUp, Guid>>().GetAsync(state.FollowUps.Single().Id);
				stored.ScheduledAt.Ticks.ShouldBe(scheduledAt.UtcDateTime.Ticks);
			});
			var row = (await _applicationAppService.GetPipelineAsync(new() { MyCases = false, AdmissionOfferingId = offering.Id })).Items.Single();
			row.OpenFollowUpCount.ShouldBe(1);
			row.NextFollowUpPurpose.ShouldBe("Confirm visit");
			row.OverdueFollowUpCount.ShouldBe(0);
			row.NextFollowUpAt.ShouldBe(scheduledAt.UtcDateTime);
			row.NextFollowUpAt!.Value.Kind.ShouldBe(DateTimeKind.Utc);
			var completionStartedAt = DateTime.UtcNow;
			state = await _applicationAppService.CompleteFollowUpAsync(application.Id, state.FollowUps.Single().Id, new()
			{
				ConcurrencyStamp = state.ConcurrencyStamp, FollowUpConcurrencyStamp = state.FollowUps.Single().ConcurrencyStamp, Note = "Visit confirmed"
			});
			state.FollowUps.Single().Status.ShouldBe(AdmissionInquiryFollowUpStatus.Completed);
			state.FollowUps.Single().CompletedAt!.Value.Kind.ShouldBe(DateTimeKind.Utc);
			state.FollowUps.Single().CompletedAt!.Value.ShouldBeInRange(completionStartedAt.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
			state = await _applicationAppService.GetFollowUpsAsync(application.Id);
			state.FollowUps.Single().CompletedAt!.Value.ShouldBeInRange(completionStartedAt.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
			state = await _applicationAppService.ScheduleFollowUpAsync(application.Id, new() { ConcurrencyStamp = state.ConcurrencyStamp, ScheduledAt = DateTimeOffset.UtcNow.AddMinutes(30), Purpose = "Admission documents" });
			state.FollowUps.Count.ShouldBe(2);
			row = (await _applicationAppService.GetPipelineAsync(new() { MyCases = false, AdmissionOfferingId = offering.Id })).Items.Single();
			row.HasOverdueFollowUp.ShouldBeFalse();
			await WithUnitOfWorkAsync(async () =>
			{
				var repository = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<AdmissionInquiryFollowUp, Guid>>();
				var followUp = await repository.GetAsync(state.FollowUps.Single(item => item.Status == AdmissionInquiryFollowUpStatus.Scheduled).Id);
				typeof(AdmissionInquiryFollowUp).GetProperty(nameof(AdmissionInquiryFollowUp.ScheduledAt))!.SetValue(followUp, DateTime.UtcNow.AddMinutes(-30));
				await repository.UpdateAsync(followUp, true);
			});
			(await _applicationAppService.GetPipelineAsync(new() { MyCases = false, AdmissionOfferingId = offering.Id })).Items.Single().HasOverdueFollowUp.ShouldBeTrue();
			state = await _applicationAppService.ScheduleFollowUpAsync(application.Id, new() { ConcurrencyStamp = state.ConcurrencyStamp, ScheduledAt = DateTimeOffset.UtcNow.AddDays(2), Purpose = "Later call" });
			row = (await _applicationAppService.GetPipelineAsync(new() { MyCases = false, AdmissionOfferingId = offering.Id })).Items.Single();
			row.OpenFollowUpCount.ShouldBe(2);
			row.OverdueFollowUpCount.ShouldBe(1);
			row.NextFollowUpPurpose.ShouldBe("Admission documents");
			(await Should.ThrowAsync<BusinessException>(() => management.RecordContactAsync(inquiry.Id, new()
			{
				ConcurrencyStamp = state.ConcurrencyStamp, Channel = "PHONE", Direction = "OUTBOUND", Outcome = "CONNECTED", Note = "Transferred inquiry must stay locked."
			}))).Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidState);
			await WithUnitOfWorkAsync(async () =>
			{
				var entity = await _applicationRepository.GetAsync(application.Id);
				typeof(AdmissionApplication).GetProperty(nameof(AdmissionApplication.Status))!.SetValue(entity, AdmissionApplicationStatus.AdmissionFinalized);
				await _applicationRepository.UpdateAsync(entity, true);
			});
			state = await _applicationAppService.GetFollowUpsAsync(application.Id);
			state.CanManageFollowUps.ShouldBeFalse();
			(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.ScheduleFollowUpAsync(application.Id, new() { ConcurrencyStamp = state.ConcurrencyStamp, ScheduledAt = DateTimeOffset.UtcNow.AddDays(4), Purpose = "Closed" }))).Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidState);
		});
}
