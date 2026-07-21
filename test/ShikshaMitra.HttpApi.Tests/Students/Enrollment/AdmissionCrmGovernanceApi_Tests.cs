using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Enrollments;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class AdmissionCrmGovernanceApi_Tests : ShikshaMitraApiTestBase
{
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
		var runScheduleResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/{yearId}/governance/report-schedules/run-due", null);
		runScheduleResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await runScheduleResponse.Content.ReadAsStringAsync());
		var scheduleRun = (await runScheduleResponse.Content.ReadFromJsonAsync<AdmissionCrmReportScheduleRunResultDto>(JsonOptions))!;
		scheduleRun.GeneratedCount.ShouldBe(1,
			string.Join("; ", scheduleRun.FailureDetails.Select(x => $"{x.Key}: {x.Value}")));
		scheduleRun.ExportIds.Count.ShouldBe(1);

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
		forecast.Limitations.ShouldContain(x => x.Contains("cannot make or change an admission decision"));
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
}
