using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Records_directory_selected_year_projects_placement_without_tracking_enrollment_graphs() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		Guid yearId = Guid.Empty;
		for (var index = 0; index < 4; index++)
		{
			var student = await CreateExistingStudentAsync(offering, $"ReadDirectory{index}", "Student");
			yearId = student.AcademicYearId!.Value;
			await GrantCompletedRecordAsync(student, index + 1);
		}
		var single = await MeasureRecordsReadAsync(() => GetRequiredService<IStudentDirectoryAppService>().GetListAsync(new()
		{
			AcademicYearId = yearId, Search = "ReadDirectory", MaxResultCount = 1
		}));
		var page = await MeasureRecordsReadAsync(() => GetRequiredService<IStudentDirectoryAppService>().GetListAsync(new()
		{
			AcademicYearId = yearId, Search = "ReadDirectory", MaxResultCount = 4
		}));
		page.Result.TotalCount.ShouldBe(4);
		page.Result.Items.Select(row => row.RollNo).ShouldBe(new int?[] { 1, 2, 3, 4 });
		page.Result.Items.ShouldAllBe(row => row.EntrySource == "CompletedOnboarding" && row.AcademicYearId == yearId);
		page.Enrollments.ShouldBe(0, "Directory selected-year placement is a scalar read, not an enrollment aggregate read.");
		page.Reads.ShouldBeLessThanOrEqualTo(single.Reads);
	});

	[Fact]
	public Task Records_profile_selected_year_projects_medium_without_tracking_enrollment_graphs() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "ReadProfile", "Student");
		await GrantCompletedRecordAsync(student, 23);
		var measured = await MeasureRecordsReadAsync(() => GetRequiredService<IStudentRecordProfileAppService>()
			.GetAsync(student.Id, student.AcademicYearId));
		measured.Result.RollNo.ShouldBe(23);
		measured.Result.AcademicMediumId.ShouldBe(offering.MediumId);
		measured.Result.EnrollmentHistory.Count.ShouldBe(1);
		measured.Enrollments.ShouldBe(0, "Historical medium labels must not hydrate StudentEnrollment.");
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Records_transition_history_batches_shared_page_reads_and_preserves_detail_and_privacy(bool masked) => RunCertificationScenarioAsync(async () =>
	{
		var policy = StudentRecordsPolicy.Starter();
		if (masked) policy.Access.Masks.Add(new() { FieldGroup = "Identity", AllowedRoleNames = [] });
		var fixture = await TransitionFixtureAsync(policy);
		await WithUnitOfWorkAsync(async () =>
		{
			var enrollment = await _studentEnrollmentRepository.GetAsync(e => e.StudentId == fixture.Student.Id);
			for (var index = 0; index < 4; index++)
			{
				var batch = new StudentRecordTransitionBatch(Guid.NewGuid(), TestTenantId, fixture.Input.AcademicYearId,
					$"read-history-{Guid.NewGuid():N}", "{}", "[]");
				await GetRequiredService<IRepository<StudentRecordTransitionBatch, Guid>>().InsertAsync(batch, true);
				var transition = new StudentRecordTransition(Guid.NewGuid(), TestTenantId, batch.Id, fixture.Student.Id,
					fixture.Input.AcademicYearId, enrollment.Id, "Withdraw", $"Historical request {index}", DateTime.UtcNow.Date,
					JsonSerializer.Serialize(fixture.Input.Items[0]), "{}", null, policy.ToJson(), JsonSerializer.Serialize(policy.Changes["Status"]), TestUserId, 1);
				transition.Cancel();
				await GetRequiredService<IRepository<StudentRecordTransition, Guid>>().InsertAsync(transition, true);
				await GetRequiredService<IRepository<StudentRecordTransitionEvent, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId,
					transition.Id, TestUserId, "Cancelled", 0, $"History evidence {index}"), true);
			}
		});
		var single = await MeasureRecordsReadAsync(() => fixture.Service.GetListAsync(new() { StudentId = fixture.Student.Id, MaxResultCount = 1 }));
		var page = await MeasureRecordsReadAsync(() => fixture.Service.GetListAsync(new() { StudentId = fixture.Student.Id, MaxResultCount = 4 }));
		page.Result.TotalCount.ShouldBe(4);
		page.Result.Items.Count.ShouldBe(4);
		Console.WriteLine($"Records cancelled transition history SQLite reads: page 1={single.Reads}, page 4={page.Reads}.");
		page.Reads.ShouldBeLessThanOrEqualTo(single.Reads + 2, "Page-shared policy, eligibility, roles and event reads must not repeat for each history row.");
		foreach (var row in page.Result.Items)
		{
			row.History.Count.ShouldBe(1);
			row.CanApprove.ShouldBeFalse(); row.CanApply.ShouldBeFalse(); row.CanCancel.ShouldBeFalse(); row.CanCorrect.ShouldBeFalse();
			row.StudentName.ShouldBe(masked ? null : "Transition Student");
			if (masked) row.History[0].Remarks.ShouldBeEmpty();
			else row.History[0].Remarks.ShouldStartWith("History evidence ");
			JsonSerializer.Serialize(row).ShouldBe(JsonSerializer.Serialize(await fixture.Service.GetAsync(row.Id)));
		}
		using (_currentTenant.Change(Guid.NewGuid()))
			await Should.ThrowAsync<Volo.Abp.Authorization.AbpAuthorizationException>(() => fixture.Service.GetListAsync(new() { StudentId = fixture.Student.Id }));
	});

	[Fact]
	public Task Records_document_report_projects_student_labels_without_tracking_profiles() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "ReadDocument", "Student");
		await GrantCompletedRecordAsync(student);
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IRepository<StudentRecordDocumentEvent, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId,
				student.Id, Guid.NewGuid(), TestUserId, "ID", "Issued", "Issue evidence", student.AcademicYearId), true);
		});
		var page = await MeasureRecordsReadAsync(() => GetRequiredService<IStudentRecordReportAppService>().PreviewAsync(new()
		{
			ReportType = "Documents", StudentId = student.Id, MaxResultCount = 1
		}));
		page.Result.TotalCount.ShouldBe(1);
		page.Result.Rows.Single()["Student"].ShouldBe("ReadDocument Student");
		page.Result.Rows.Single()["Reason"].ShouldBe("Issue evidence");
		page.Students.ShouldBe(0, "Document report rows need names and policy-year keys, not permanent Student aggregates.");
		page.StudentSql.ShouldNotBeEmpty();
		page.StudentSql.ShouldAllBe(sql => !sql.Contains("PhotoBlobName", StringComparison.Ordinal),
			"The document report must select only student labels and policy-year keys, excluding unrelated profile columns.");
	});

	[Fact]
	public Task Records_change_history_resolves_current_default_policy_once_per_page() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "ReadChange", "Student");
		await GrantCompletedRecordAsync(student);
		await PublishDirectoryPolicyAsync(StudentRecordsPolicy.Starter());
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			var current = await _studentRepository.GetAsync(student.Id);
			// Legacy permanent profiles may have no current year while retaining audited historical records.
			db.Entry(current).Property(s => s.AcademicYearId).CurrentValue = null;
			for (var index = 0; index < 4; index++)
			{
				var change = new StudentRecordChange(Guid.NewGuid(), TestTenantId, student.Id, student.AcademicYearId!.Value, TestUserId,
					"general", "Identity", $"read-change-{index}", "Historical identity evidence", "{}", "{}", StudentRecordsPolicy.Starter().ToJson(),
					null, "[]", 0, DateTimeOffset.UtcNow.AddDays(-1));
				change.Apply(DateTimeOffset.UtcNow);
				await GetRequiredService<IRepository<StudentRecordChange, Guid>>().InsertAsync(change, true);
			}
		});
		var service = GetRequiredService<IStudentRecordChangeAppService>();
		var single = await MeasureRecordsReadAsync(() => service.GetListAsync(new() { StudentId = student.Id, MaxResultCount = 1 }));
		var page = await MeasureRecordsReadAsync(() => service.GetListAsync(new() { StudentId = student.Id, MaxResultCount = 4 }));
		page.Result.TotalCount.ShouldBe(4);
		page.Result.Items.ShouldAllBe(row => row.StudentName == "ReadChange Student" && !row.PayloadMasked && row.Before == null && row.Proposed == null);
		page.Students.ShouldBe(0, "Change list names and current-year policy keys require scalar student metadata only.");
		page.StudentSql.ShouldAllBe(sql => !sql.Contains("PhotoBlobName", StringComparison.Ordinal),
			"Read-only Student repository queries may be untracked already; transfer must still exclude unrelated profile columns.");
		page.Reads.ShouldBeLessThanOrEqualTo(single.Reads);
	});

	private Task<(T Result, int Reads, int Students, int Enrollments, string[] StudentSql)> MeasureRecordsReadAsync<T>(Func<Task<T>> read) => WithUnitOfWorkAsync(async () =>
	{
		var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		db.ChangeTracker.Clear();
		using var counter = new AcademicQueryCounter(db.Database.GetDbConnection());
		var result = await read();
		return (result, counter.ReadCount, db.ChangeTracker.Entries<Student>().Count(), db.ChangeTracker.Entries<StudentEnrollment>().Count(),
			counter.ReadCommands.Where(sql => sql.Contains("AppStudents", StringComparison.Ordinal)).ToArray());
	});
}
