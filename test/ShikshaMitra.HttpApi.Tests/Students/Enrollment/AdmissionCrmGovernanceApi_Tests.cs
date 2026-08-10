using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Enrollments;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class AdmissionCrmGovernanceApi_Tests : ShikshaMitraApiTestBase
{
	private static int _academicYearSequence;

	public AdmissionCrmGovernanceApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Govern_Retention_Privacy_Exports_Delegation_Audit_And_Forecast()
	{
		var yearId = Factory.SeededAcademicYearId;
		var policyResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{yearId}/governance/retention-policies",
			new CreateAdmissionCrmRetentionPolicyDto
			{
				Code = "UNSUCCESSFUL-" + Guid.NewGuid().ToString("N")[..6],
				Name = "Unsuccessful applicant retention",
				Category = AdmissionCrmRetentionCategory.UnsuccessfulApplicant,
				Action = AdmissionCrmRetentionAction.Anonymize,
				RetainDays = 365,
				AuthorityNote = "School-approved retention schedule subject to applicable law."
			}, JsonOptions);
		policyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await policyResponse.Content.ReadAsStringAsync());
		var policy = (await policyResponse.Content.ReadFromJsonAsync<AdmissionCrmRetentionPolicyDto>(JsonOptions))!;

		var submitResponse = await Client.PostAsync($"/api/student-enrollment/admissions-crm/governance/retention-policies/{policy.Id}/submit", null);
		submitResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await submitResponse.Content.ReadAsStringAsync());
		var sameMakerPublish = await Client.PostAsync($"/api/student-enrollment/admissions-crm/governance/retention-policies/{policy.Id}/publish", null);
		sameMakerPublish.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await sameMakerPublish.Content.ReadAsStringAsync()).ShouldContain("AdmissionCrm:DistinctRetentionPolicyApproverRequired");
		await PublishRetentionAsDifferentApproverAsync(policy.Id);

		var lead = await CreateLeadAsync();
		var privacyResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/governance/privacy-requests",
			new CreateAdmissionCrmPrivacyRequestDto
			{
				Type = AdmissionCrmPrivacyRequestType.Anonymization,
				Reason = "Parent requested a governed review of pre-admission personal data."
			}, JsonOptions);
		privacyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await privacyResponse.Content.ReadAsStringAsync());
		var privacy = (await privacyResponse.Content.ReadFromJsonAsync<AdmissionCrmPrivacyRequestDto>(JsonOptions))!;
		privacy.Preview.RequiresManualExecution.ShouldBeTrue();
		privacy.Preview.ApplicablePolicyIds.ShouldContain(policy.Id);

		var auditResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/governance/audit?academicYearId={yearId}&leadId={lead.Id}&eventType={(int)AdmissionCrmTimelineEventType.PrivacyRequestCreated}");
		auditResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await auditResponse.Content.ReadAsStringAsync());
		var audit = (await auditResponse.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmTimelineEventDto>>(JsonOptions))!;
		audit.TotalCount.ShouldBe(1);

		var exportResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{yearId}/governance/exports",
			new RequestAdmissionCrmExportDto { ReportType = AdmissionCrmExportReportType.AuditTimeline, ExpiresInHours = 1 }, JsonOptions);
		exportResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await exportResponse.Content.ReadAsStringAsync());
		var export = (await exportResponse.Content.ReadFromJsonAsync<AdmissionCrmExportRequestDto>(JsonOptions))!;
		export.Status.ShouldBe(AdmissionCrmExportStatus.Ready);
		var downloadResponse = await Client.GetAsync($"/api/student-enrollment/admissions-crm/governance/exports/{export.Id}/download");
		downloadResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await downloadResponse.Content.ReadAsStringAsync());
		var download = (await downloadResponse.Content.ReadFromJsonAsync<AdmissionCrmExportDownloadDto>(JsonOptions))!;
		var csv = Encoding.UTF8.GetString(Convert.FromBase64String(download.Base64Content));
		csv.ShouldContain("\"AcademicYearId\",\"" + yearId + "\"");
		csv.ShouldContain("\"DefinitionVersion\",\"1\"");
		csv.ShouldContain("PrivacyRequestCreated");

		var scheduleResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{yearId}/governance/report-schedules",
			new CreateAdmissionCrmReportScheduleDto
			{
				Name = "Daily government readiness review",
				ReportType = AdmissionCrmExportReportType.GovernmentReadiness,
				Frequency = AdmissionCrmReportFrequency.Daily,
				Destination = "manual:secure-export-queue",
				NextRunAt = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
			}, JsonOptions);
		scheduleResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await scheduleResponse.Content.ReadAsStringAsync());
		var createdSchedule = (await scheduleResponse.Content
			.ReadFromJsonAsync<AdmissionCrmReportScheduleDto>(JsonOptions))!;
		var unapprovedRecipientResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{yearId}/governance/report-schedules",
			new CreateAdmissionCrmReportScheduleDto
			{
				Name = "Unapproved recipient",
				ReportType = AdmissionCrmExportReportType.Funnel,
				Frequency = AdmissionCrmReportFrequency.Weekly,
				Destination = $"user:{Guid.NewGuid()}",
				NextRunAt = DateTime.UtcNow.AddDays(1)
			}, JsonOptions);
		unapprovedRecipientResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await unapprovedRecipientResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:ScheduledReportRecipientMustBeAdmissionsStaff");
		var runScheduleResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/{yearId}/governance/report-schedules/run-due", null);
		runScheduleResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await runScheduleResponse.Content.ReadAsStringAsync());
		var scheduleRun = (await runScheduleResponse.Content.ReadFromJsonAsync<AdmissionCrmReportScheduleRunResultDto>(JsonOptions))!;
		scheduleRun.GeneratedCount.ShouldBeGreaterThanOrEqualTo(1,
			string.Join("; ", scheduleRun.FailureDetails.Select(x => $"{x.Key}: {x.Value}")));
		scheduleRun.ExportIds.Count.ShouldBe(scheduleRun.GeneratedCount);
		scheduleRun.FailedScheduleIds.ShouldNotContain(createdSchedule.Id);
		var schedulesResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{yearId}/governance/report-schedules");
		schedulesResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await schedulesResponse.Content.ReadAsStringAsync());
		var schedules = (await schedulesResponse.Content
			.ReadFromJsonAsync<IReadOnlyList<AdmissionCrmReportScheduleDto>>(JsonOptions))!;
		schedules.Single(x => x.Id == createdSchedule.Id).LastRunAt.ShouldNotBeNull();

		var owners = await GetEligibleCrmOwnersAsync();
		var delegator = owners[0].Id;
		var delegateUser = owners[1].Id;
		var delegationResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{yearId}/operations/delegations",
			new CreateAdmissionCrmWorkDelegationDto
			{
				DelegatorUserId = delegator,
				DelegateUserId = delegateUser,
				QueueTypes = [AdmissionCrmOperationsQueueType.UnassignedExam],
				StartsAt = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
				EndsAt = new DateTime(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc),
				Reason = "Document reviewer leave coverage."
			}, JsonOptions);
		delegationResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await delegationResponse.Content.ReadAsStringAsync());
		var delegation = (await delegationResponse.Content.ReadFromJsonAsync<AdmissionCrmWorkDelegationDto>(JsonOptions))!;
		delegation.Status.ShouldBe(AdmissionCrmDelegationStatus.Active);
		var assignResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/owner",
			new AssignAdmissionCrmLeadOwnerDto { OwnerUserId = delegator }, JsonOptions);
		assignResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await assignResponse.Content.ReadAsStringAsync());
		foreach (var stage in new[] { AdmissionInquiryStage.FollowUp, AdmissionInquiryStage.ExamReady })
		{
			var stageResponse = await Client.PostAsJsonAsync(
				$"/api/student-enrollment/admissions-crm/leads/{lead.Id}/stage",
				new ChangeAdmissionCrmLeadStageDto { Stage = stage, Reason = "Governance queue delegation test." }, JsonOptions);
			stageResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await stageResponse.Content.ReadAsStringAsync());
		}
		var delegatedQueueResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{yearId}/operations/queues/{(int)AdmissionCrmOperationsQueueType.UnassignedExam}?assignedToUserId={delegateUser}&maxResultCount=50");
		delegatedQueueResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await delegatedQueueResponse.Content.ReadAsStringAsync());
		var delegatedQueue = (await delegatedQueueResponse.Content.ReadFromJsonAsync<AdmissionCrmOperationsQueueDto>(JsonOptions))!;
		delegatedQueue.Items.Select(x => x.LeadId).ShouldContain(lead.Id);

		var savedViewResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{yearId}/operations/saved-views",
			new CreateAdmissionCrmOperationsSavedViewDto
			{
				Name = "My delegated urgent exams " + Guid.NewGuid().ToString("N")[..6],
				QueueType = AdmissionCrmOperationsQueueType.UnassignedExam,
				Filters = new GetAdmissionCrmOperationsQueueInput { AssignedToUserId = delegateUser, UrgentOnly = true, MaxResultCount = 50 }
			}, JsonOptions);
		savedViewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await savedViewResponse.Content.ReadAsStringAsync());

		var forecastResponse = await Client.GetAsync($"/api/student-enrollment/admissions-crm/{yearId}/analytics/advisory-forecast");
		forecastResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await forecastResponse.Content.ReadAsStringAsync());
		var forecast = (await forecastResponse.Content.ReadFromJsonAsync<AdmissionCrmForecastDto>(JsonOptions))!;
		forecast.IsAdvisory.ShouldBeTrue();
		forecast.RemainingSeatCapacity.ShouldBeGreaterThanOrEqualTo(0);
		forecast.ProjectedSeatSurplus.ShouldBe(
			forecast.RemainingSeatCapacity - forecast.ProjectedAdditionalActivations);
		forecast.CapacityIsScopeComplete.ShouldBeTrue();
		forecast.FirstResponseAtRiskCount.ShouldBeGreaterThanOrEqualTo(0);
		forecast.Limitations.ShouldContain(x => x.Contains("cannot make or change an admission decision"));
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Require_Academic_Year_Wide_Scope_For_Retention_Policy_Mutations()
	{
		using var standardScopedClient = await CreateStandardScopedClientAsync();
		var yearId = Factory.SeededAcademicYearId;
		var deniedCreateResponse = await standardScopedClient.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{yearId}/governance/retention-policies",
			new CreateAdmissionCrmRetentionPolicyDto
			{
				Code = "DENIED-" + Guid.NewGuid().ToString("N")[..6],
				Name = "Denied standard-scoped retention policy",
				Category = AdmissionCrmRetentionCategory.UnsuccessfulApplicant,
				Action = AdmissionCrmRetentionAction.Anonymize,
				RetainDays = 365,
				AuthorityNote = "Standard-scoped users cannot govern the full academic year."
			},
			JsonOptions);
		deniedCreateResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await deniedCreateResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:AcademicYearWideScopeRequired");
		var scopedForecastResponse = await standardScopedClient.GetAsync(
			$"/api/student-enrollment/admissions-crm/{yearId}/analytics/advisory-forecast");
		scopedForecastResponse.StatusCode.ShouldBe(
			HttpStatusCode.OK,
			await scopedForecastResponse.Content.ReadAsStringAsync());
		var scopedForecast = await scopedForecastResponse.Content
			.ReadFromJsonAsync<AdmissionCrmForecastDto>(JsonOptions);
		scopedForecast.ShouldNotBeNull();
		scopedForecast.CapacityIsScopeComplete.ShouldBeFalse();
		scopedForecast.ApprovedSeatCapacity.ShouldBe(0);
		scopedForecast.RemainingSeatCapacity.ShouldBe(0);

		var createResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{yearId}/governance/retention-policies",
			new CreateAdmissionCrmRetentionPolicyDto
			{
				Code = "SCOPE-" + Guid.NewGuid().ToString("N")[..6],
				Name = "Academic-year retention policy",
				Category = AdmissionCrmRetentionCategory.UnsuccessfulApplicant,
				Action = AdmissionCrmRetentionAction.Anonymize,
				RetainDays = 365,
				AuthorityNote = "Academic-year governance regression coverage."
			},
			JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
		var policy = await createResponse.Content.ReadFromJsonAsync<AdmissionCrmRetentionPolicyDto>(JsonOptions);
		policy.ShouldNotBeNull();

		var deniedSubmitResponse = await standardScopedClient.PostAsync(
			$"/api/student-enrollment/admissions-crm/governance/retention-policies/{policy.Id}/submit",
			content: null);
		deniedSubmitResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await deniedSubmitResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:AcademicYearWideScopeRequired");

		var submitResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/governance/retention-policies/{policy.Id}/submit",
			content: null);
		submitResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await submitResponse.Content.ReadAsStringAsync());

		var deniedPublishResponse = await standardScopedClient.PostAsync(
			$"/api/student-enrollment/admissions-crm/governance/retention-policies/{policy.Id}/publish",
			content: null);
		deniedPublishResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await deniedPublishResponse.Content.ReadAsStringAsync())
			.ShouldContain("AdmissionCrm:AcademicYearWideScopeRequired");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Run_Scheduled_Report_Automation_Without_A_User_And_Reject_Cross_Tenant_Year()
	{
		var yearId = Factory.SeededAcademicYearId;
		var scheduleResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{yearId}/governance/report-schedules",
			new CreateAdmissionCrmReportScheduleDto
			{
				Name = "No-user automation " + Guid.NewGuid().ToString("N")[..8],
				ReportType = AdmissionCrmExportReportType.GovernmentReadiness,
				Frequency = AdmissionCrmReportFrequency.Daily,
				Destination = "manual:secure-export-queue",
				NextRunAt = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
			},
			JsonOptions);
		scheduleResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await scheduleResponse.Content.ReadAsStringAsync());
		var schedule = await scheduleResponse.Content.ReadFromJsonAsync<AdmissionCrmReportScheduleDto>(JsonOptions);
		schedule.ShouldNotBeNull();
		var otherTenantYearId = await CreateAcademicYearAsync(Guid.NewGuid());

		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var currentPrincipalAccessor = scope.ServiceProvider.GetRequiredService<ICurrentPrincipalAccessor>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var governanceCoordinator = scope.ServiceProvider.GetRequiredService<AdmissionCrmGovernanceAutomationCoordinator>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using (currentPrincipalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity())))
		using (currentTenant.Change(tenantId))
		{
			using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
			{
				var result = await governanceCoordinator.RunDueReportSchedulesAsync(yearId);
				result.GeneratedCount.ShouldBeGreaterThanOrEqualTo(1);
				result.FailedScheduleIds.ShouldNotContain(schedule.Id);
				await uow.CompleteAsync();
			}

			using (uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
			{
				var exception = await Should.ThrowAsync<BusinessException>(() =>
					governanceCoordinator.RunDueReportSchedulesAsync(otherTenantYearId));
				exception.Code.ShouldBe("AdmissionCrm:AcademicYearNotFound");
			}
		}
	}

	private async Task<AdmissionCrmLeadDto> CreateLeadAsync()
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "Grade 1 admission",
				DesiredClassName = "Grade 1",
				PrimaryContactNo = "9" + Random.Shared.NextInt64(100000000, 999999999),
				Channel = AdmissionInquiryChannel.QuickCallEntry,
				FirstName = "Governance" + Guid.NewGuid().ToString("N")[..6]
			}, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions))!;
	}

	private async Task PublishRetentionAsDifferentApproverAsync(Guid id)
	{
		using var scope = Factory.Services.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<AdmissionCrmRetentionPolicy, Guid>>();
		using (tenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var item = await repository.GetAsync(id);
			item.Publish(Guid.NewGuid(), DateTime.UtcNow);
			await repository.UpdateAsync(item);
			await uow.CompleteAsync();
		}
	}

	private async Task<HttpClient> CreateStandardScopedClientAsync()
	{
		var userId = Guid.NewGuid();
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var scopeRepository = scope.ServiceProvider
			.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		var userScopeService = scope.ServiceProvider.GetRequiredService<IUserScopeService>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await scopeRepository.InsertAsync(
				new UserScopeAssignment(
					Guid.NewGuid(),
					tenantId,
					userId: userId,
					academicYearId: Factory.SeededAcademicYearId,
					standardId: Factory.SeededStandardId,
					description: "Admissions retention standard-only scope regression."),
				autoSave: false);
			await userScopeService.InvalidateUserScopeCacheAsync(userId);
			await uow.CompleteAsync();
		}

		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, userId.ToString("D"));
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "teacher");
		return client;
	}

	private async Task<Guid> CreateAcademicYearAsync(Guid tenantId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var academicYearRepository = scope.ServiceProvider
			.GetRequiredService<IRepository<AcademicYear, Guid>>();
		var academicYearManager = scope.ServiceProvider.GetRequiredService<AcademicYearManager>();
		var year = 3000 + Interlocked.Increment(ref _academicYearSequence) * 2;

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var tenantToken = tenantId.ToString("N")[..12];
			var academicYearName = $"CRM Governance {tenantToken} {year}";
			var academicYear = await academicYearManager.CreateAsync(
				academicYearName,
				new DateTime(year, 4, 1),
				new DateTime(year + 1, 3, 31));
			await academicYearRepository.InsertAsync(academicYear, autoSave: false);
			await uow.CompleteAsync();
			return academicYear.Id;
		}
	}
}
