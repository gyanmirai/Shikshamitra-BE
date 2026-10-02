using System;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Record_report_generates_real_excel_and_rechecks_policy_before_download() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordContactFixtureAsync();
		var reports = GetRequiredService<IStudentRecordReportAppService>();
		var request = new StudentRecordReportInput { ReportType = "Directory", StudentId = fixture.Student.Id, ClientKey = Guid.NewGuid().ToString("N") };
		var preview = await reports.PreviewAsync(request); preview.TotalCount.ShouldBe(1); preview.Rows[0]["Student"].ShouldBe("RecordChange Student");
		var queued = await reports.CreateAsync(request); queued.Status.ShouldBe("Queued"); queued.CanDownload.ShouldBeFalse();
		(await reports.CreateAsync(request)).Id.ShouldBe(queued.Id);
		await GetRequiredService<StudentRecordReportRunner>().RunAsync(queued.Id);
		var file = await reports.DownloadAsync(queued.Id);
		file.Content.Length.ShouldBeGreaterThan(1000); file.Content[0].ShouldBe((byte)'P'); file.Content[1].ShouldBe((byte)'K');
		file.ContentType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
		await PublishDirectoryPolicyAsync(StudentRecordsPolicy.Starter());
		(await Should.ThrowAsync<BusinessException>(() => reports.DownloadAsync(queued.Id))).Code.ShouldBe("Students:Records:ReportAuthorityChanged");
	});
	[Fact]
	public Task Record_cancelled_report_cannot_generate_or_download() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordContactFixtureAsync();
		var reports = GetRequiredService<IStudentRecordReportAppService>();
		var queued = await reports.CreateAsync(new() { ReportType = "Directory", StudentId = fixture.Student.Id, ClientKey = Guid.NewGuid().ToString("N") });
		(await reports.CancelAsync(queued.Id)).Status.ShouldBe("Cancelled");
		await GetRequiredService<StudentRecordReportRunner>().RunAsync(queued.Id);
		(await Should.ThrowAsync<BusinessException>(() => reports.DownloadAsync(queued.Id))).Code.ShouldBe("Students:Records:ReportNotReady");
	});
}
