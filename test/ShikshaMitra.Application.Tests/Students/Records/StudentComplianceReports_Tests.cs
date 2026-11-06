using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.Compliance;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("UdiseBaseline")]
	[InlineData("GujaratCompliance")]
	[InlineData("IdentityExceptions")]
	[InlineData("EligibilityTracking")]
	public Task Compliance_report_previews_stored_masked_facts_and_exports_real_workbook(string type) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordContactFixtureAsync();
		if (type == "GujaratCompliance") await SetPortalFixturePropertyAsync<EducationBoard>(fixture.Student.AcademicBoardId!.Value, nameof(EducationBoard.Type), EducationBoardType.GSEB);
		await WithUnitOfWorkAsync(async () =>
		{
			var compliance = new StudentIdentityCompliance(Guid.NewGuid(), TestTenantId, fixture.Student.Id);
			compliance.SetAadhaar("encrypted-not-for-reports", "private-hash", "1234", "Private document name", IdentityVerificationStatus.Mismatch);
			compliance.SetApaar("123456789012", ConsentStatus.Refused, DateTimeOffset.UtcNow, IdentityVerificationStatus.Pending);
			compliance.SetAdministrativeIdentifiers("PEN12345678", "UDISE12345678", UdiseSyncStatus.NeedsCorrection);
			compliance.SetBenefitTracking(true, ScholarshipStatus.Applied, "Saved scholarship scheme", "APP12345678", "CAST12345678", IdentityVerificationStatus.Pending);
			await GetRequiredService<IRepository<StudentIdentityCompliance, Guid>>().InsertAsync(compliance, true);
		});
		var reports = GetRequiredService<IStudentRecordReportAppService>();
		var input = new StudentRecordReportInput { ReportType = type, AcademicYearId = fixture.Student.AcademicYearId, StudentId = fixture.Student.Id, ClientKey = Guid.NewGuid().ToString("N") };
		var preview = await reports.PreviewAsync(input);
		preview.TotalCount.ShouldBe(1);
		preview.Rows.Single()["Student"].ShouldBe("RecordChange Student");
		var json = System.Text.Json.JsonSerializer.Serialize(preview);
		json.ShouldNotContain("encrypted-not-for-reports"); json.ShouldNotContain("private-hash");
		json.ShouldNotContain("123456789012"); json.ShouldNotContain("PEN12345678"); json.ShouldNotContain("APP12345678");
		if (type == "EligibilityTracking") preview.Rows.Single()["Scholarship status"].ShouldBe("Applied");
		else preview.Rows.Single()["Aadhaar verification"].ShouldBe("Mismatch");
		var queued = await reports.CreateAsync(input); queued.Status.ShouldBe("Queued");
		(await reports.CreateAsync(input)).Id.ShouldBe(queued.Id);
		await GetRequiredService<StudentRecordReportRunner>().RunAsync(queued.Id);
		var file = await reports.DownloadAsync(queued.Id);
		file.Content[0].ShouldBe((byte)'P'); file.Content[1].ShouldBe((byte)'K');
		var xml = ReadReportProofXml(file.Content);
		xml.ShouldContain("RecordChange Student"); xml.ShouldNotContain("PEN12345678"); xml.ShouldNotContain("123456789012");
		await PublishDirectoryPolicyAsync(StudentRecordsPolicy.Starter());
		(await Should.ThrowAsync<BusinessException>(() => reports.DownloadAsync(queued.Id))).Code.ShouldBe("Students:Records:ReportAuthorityChanged");
	});

	[Fact]
	public Task Compliance_report_requires_explicit_year() => RunCertificationScenarioAsync(async () =>
	{
		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IStudentRecordReportAppService>().PreviewAsync(new() { ReportType = "UdiseBaseline" })))
			.Code.ShouldBe("Students:Records:InvalidReport");
	});

	[Fact]
	public Task Compliance_report_uses_current_academic_scope_and_keeps_filters_in_job_request() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordContactFixtureAsync();
		var reports = GetRequiredService<IStudentRecordReportAppService>();
		var input = new StudentRecordReportInput { ReportType = "UdiseBaseline", AcademicYearId = fixture.Student.AcademicYearId,
			StudentId = fixture.Student.Id, BoardId = fixture.Student.AcademicBoardId, MediumId = fixture.Student.AcademicMediumId,
			StandardId = fixture.Student.StandardId, ClientKey = Guid.NewGuid().ToString("N") };
		(await reports.PreviewAsync(input)).TotalCount.ShouldBe(1);
		var job = await reports.CreateAsync(input);
		job.Request.BoardId.ShouldBe(input.BoardId); job.Request.MediumId.ShouldBe(input.MediumId); job.Request.StandardId.ShouldBe(input.StandardId);
		input.MediumId = Guid.NewGuid(); (await reports.PreviewAsync(input)).TotalCount.ShouldBe(0);
		input.MediumId = fixture.Student.AcademicMediumId; input.StandardId = Guid.NewGuid(); (await reports.PreviewAsync(input)).TotalCount.ShouldBe(0);
		input.StandardId = fixture.Student.StandardId; input.DivisionId = Guid.NewGuid(); (await reports.PreviewAsync(input)).TotalCount.ShouldBe(0);
		input.DivisionId = null; input.AcademicYearId = Guid.NewGuid(); (await reports.PreviewAsync(input)).TotalCount.ShouldBe(0);
		var actor = Guid.NewGuid(); await EnsureTestIdentityUserAsync(actor, $"compliance-noscope-{actor:N}");
		using (_principalAccessor.Change(ProfilePrincipal(actor)))
			await Should.ThrowAsync<AbpAuthorizationException>(() => reports.PreviewAsync(input));
		using (_currentTenant.Change(Guid.NewGuid()))
			await Should.ThrowAsync<AbpAuthorizationException>(() => reports.PreviewAsync(input));
	});

	[Fact]
	public Task Compliance_report_GSEB_and_exception_membership_use_stored_board_type_and_statuses() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordContactFixtureAsync();
		var reports = GetRequiredService<IStudentRecordReportAppService>();
		var input = new StudentRecordReportInput { ReportType = "GujaratCompliance", AcademicYearId = fixture.Student.AcademicYearId, StudentId = fixture.Student.Id };
		await SetPortalFixturePropertyAsync<EducationBoard>(fixture.Student.AcademicBoardId!.Value, nameof(EducationBoard.Type), EducationBoardType.CBSE);
		(await reports.PreviewAsync(input)).TotalCount.ShouldBe(0);
		await SetPortalFixturePropertyAsync<EducationBoard>(fixture.Student.AcademicBoardId.Value, nameof(EducationBoard.Type), EducationBoardType.GSEB);
		(await reports.PreviewAsync(input)).TotalCount.ShouldBe(1);
		input.ReportType = "IdentityExceptions"; (await reports.PreviewAsync(input)).TotalCount.ShouldBe(1);
		await WithUnitOfWorkAsync(async () =>
		{
			var compliance = new StudentIdentityCompliance(Guid.NewGuid(), TestTenantId, fixture.Student.Id);
			compliance.SetAadhaar("encrypted", "hash", "1234", null, IdentityVerificationStatus.Verified);
			compliance.SetApaar("123456789012", ConsentStatus.Granted, DateTimeOffset.UtcNow, IdentityVerificationStatus.Verified);
			await GetRequiredService<IRepository<StudentIdentityCompliance, Guid>>().InsertAsync(compliance, true);
		});
		(await reports.PreviewAsync(input)).TotalCount.ShouldBe(0);
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<StudentIdentityCompliance, Guid>>();
			var compliance = await repository.GetAsync(x => x.StudentId == fixture.Student.Id);
			compliance.SetApaarConsent(ConsentStatus.Refused, DateTimeOffset.UtcNow); await repository.UpdateAsync(compliance, true);
		});
		(await reports.PreviewAsync(input)).TotalCount.ShouldBe(1);
	});

	[Theory]
	[InlineData("Identity", "UdiseBaseline", "Student")]
	[InlineData("Contact", "UdiseBaseline", "Class")]
	[InlineData("Medical", "EligibilityTracking", "Scholarship status")]
	public Task Compliance_report_applies_canonical_source_group_masks(string group, string type, string column) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordContactFixtureAsync();
		var policy = StudentRecordsPolicy.Starter(); policy.Access.Masks = [new() { FieldGroup = group, AllowedRoleNames = ["not-the-current-reader"] }];
		await PublishDirectoryPolicyAsync(policy);
		var reports = GetRequiredService<IStudentRecordReportAppService>();
		var input = new StudentRecordReportInput { ReportType = type, AcademicYearId = fixture.Student.AcademicYearId, StudentId = fixture.Student.Id };
		(await reports.PreviewAsync(input)).Rows.Single()[column].ShouldBe(column == "Student" ? "Restricted student" : "Restricted");
		if (group is "Identity" or "Contact")
		{
			input.ReportType = "IdentityExceptions";
			(await Should.ThrowAsync<BusinessException>(() => reports.PreviewAsync(input))).Code.ShouldBe("Students:Records:RestrictedFields");
		}
	});

	[Theory]
	[InlineData("UdiseBaseline")]
	[InlineData("GujaratCompliance")]
	[InlineData("IdentityExceptions")]
	[InlineData("EligibilityTracking")]
	public Task Compliance_report_requires_identity_grant_and_rechecks_it_before_download(string type) => RunCertificationScenarioAsync(async () =>
	{
		var proof = await CreateReportReleaseProofAsync();
		using var actor = _principalAccessor.Change(ProfilePrincipal(proof.ActorId));
		var student = await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(proof.StudentId));
		var input = new StudentRecordReportInput { ReportType = type, AcademicYearId = student.AcademicYearId, StudentId = student.Id, ClientKey = Guid.NewGuid().ToString("N") };
		await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() => proof.Reports.PreviewAsync(input)));
		await SetReportProofGrantAsync(proof.ActorId, ShikshaMitraSubmodulePermissions.Students.IdentityCompliance.View, true);
		await WithUnitOfWorkAsync(() => proof.Reports.PreviewAsync(input));
		var job = await WithUnitOfWorkAsync(() => proof.Reports.CreateAsync(input)); await proof.Runner.RunAsync(job.Id);
		(await WithUnitOfWorkAsync(() => proof.Reports.DownloadAsync(job.Id))).Content.Length.ShouldBeGreaterThan(1000);
		await SetReportProofGrantAsync(proof.ActorId, ShikshaMitraSubmodulePermissions.Students.IdentityCompliance.View, false);
		await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() => proof.Reports.DownloadAsync(job.Id)));
	});
}
