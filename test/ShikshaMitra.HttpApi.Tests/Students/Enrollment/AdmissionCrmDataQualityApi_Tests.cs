using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace ShikshaMitra.Students.Enrollments;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class AdmissionCrmDataQualityApi_Tests : ShikshaMitraApiTestBase
{
	public AdmissionCrmDataQualityApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Detect_Assign_And_Resolve_Duplicate_Applicant_Issue()
	{
		var contact = "9" + Random.Shared.NextInt64(100000000, 999999999);
		await CreateLeadAsync(contact, null);
		await CreateLeadAsync(contact, "Parent confirmed this is a separate application for a sibling.");

		var runResponse = await Client.PostAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/governance/reconciliation/run", null);
		runResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await runResponse.Content.ReadAsStringAsync());
		var run = (await runResponse.Content.ReadFromJsonAsync<AdmissionCrmReconciliationResultDto>(JsonOptions))!;
		run.DetectedCount.ShouldBeGreaterThan(0);
		run.CountsByType[nameof(AdmissionCrmDataQualityIssueType.DuplicateApplicant)].ShouldBeGreaterThan(0);

		var listResponse = await Client.GetAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/governance/data-quality?status=0&issueType=0&maxResultCount=50");
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await listResponse.Content.ReadAsStringAsync());
		var result = (await listResponse.Content.ReadFromJsonAsync<PagedResultDto<AdmissionCrmDataQualityIssueDto>>(JsonOptions))!;
		var issue = result.Items.First(x => x.Details.TryGetValue("matchKey", out var matchKey) && matchKey == contact);

		var acknowledgeResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/governance/data-quality/{issue.Id}/acknowledge",
			new AcknowledgeAdmissionCrmDataQualityIssueDto(), JsonOptions);
		acknowledgeResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await acknowledgeResponse.Content.ReadAsStringAsync());
		var acknowledged = (await acknowledgeResponse.Content.ReadFromJsonAsync<AdmissionCrmDataQualityIssueDto>(JsonOptions))!;
		acknowledged.Status.ShouldBe(AdmissionCrmDataQualityIssueStatus.Acknowledged);

		var resolveResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/governance/data-quality/{issue.Id}/resolve",
			new ResolveAdmissionCrmDataQualityIssueDto
			{
				Ignore = true,
				Reason = "Admissions coordinator verified that the shared household contact is valid."
			}, JsonOptions);
		resolveResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await resolveResponse.Content.ReadAsStringAsync());
		var resolved = (await resolveResponse.Content.ReadFromJsonAsync<AdmissionCrmDataQualityIssueDto>(JsonOptions))!;
		resolved.Status.ShouldBe(AdmissionCrmDataQualityIssueStatus.Ignored);
		resolved.ResolutionReason.ShouldContain("shared household contact");
	}

	private async Task CreateLeadAsync(string contact, string? duplicateReason)
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = "Grade 1 admission",
				DesiredClassName = "Grade 1",
				PrimaryContactNo = contact,
				Channel = AdmissionInquiryChannel.QuickCallEntry,
				FirstName = "Quality" + Guid.NewGuid().ToString("N")[..6],
				DuplicateContinueReason = duplicateReason
			}, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
	}
}
