using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Records_form_save_reuses_immutable_layout_and_keeps_current_answers_and_masked_views_fresh(bool masked) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(direct: false, structuredCondition: true);
		var original = await WithUnitOfWorkAsync(async () => (await GetRequiredService<IRepository<AdmissionFormSubmission, Guid>>()
			.GetAsync(fixture.SubmissionId)).SnapshotJson);
		if (masked)
		{
			var policy = StudentRecordsPolicy.Starter(); policy.Access.Masks = [new() { FieldGroup = "Bank", AllowedRoleNames = ["other-role"] }];
			await SetInlineFormPolicyAsync(policy, 2);
		}
		var service = GetRequiredService<IStudentRecordFormAppService>();
		var input = InlineFormEdit(await service.GetSnapshotAsync(fixture.Student.Id), ("CANONICAL_BLOOD", JsonValue.Create("O+")));
		var measured = await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			db.ChangeTracker.Clear();
			using var counter = new AcademicQueryCounter(db.Database.GetDbConnection());
			var result = await service.SaveAsync(fixture.Student.Id, input);
			var lineageReads = counter.ReadCommands.Count(sql => sql.Contains("FROM \"AppAdmissionFormDefinitions\"", StringComparison.Ordinal) &&
				sql.Contains("\"ParentVersionId\"", StringComparison.Ordinal) && !sql.Contains("\"Name\"", StringComparison.Ordinal));
			return (Result: result, LineageReads: lineageReads);
		});
		measured.Result.Status.ShouldBe("Applied"); measured.Result.RequiredApprovals.ShouldBe(0);
		measured.Result.Before!.Value.GetProperty("sources")[0].GetProperty("values").GetProperty("CANONICAL_BLOOD").GetString().ShouldBe("B+");
		measured.Result.Proposed!.Value.GetProperty("sources")[0].GetProperty("values").GetProperty("CANONICAL_BLOOD").GetString().ShouldBe("O+");
		var fresh = (await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(fixture.Student.Id)).FormSources.Single();
		fresh.CurrentValues["CANONICAL_BLOOD"].GetString().ShouldBe("O+");
		fresh.OriginalValues["CANONICAL_BLOOD"].GetString().ShouldBe("B+");
		if (masked)
		{
			fresh.CurrentValues["BLOOD"].GetString().ShouldBe("Restricted");
			await SetInlineFormPolicyAsync(StudentRecordsPolicy.Starter(), 3);
			var unmasked = (await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(fixture.Student.Id)).FormSources.Single();
			unmasked.CurrentValues["BLOOD"].GetString().ShouldBe("B+");
			unmasked.CurrentValues["CANONICAL_BLOOD"].GetString().ShouldBe("O+");
			unmasked.OriginalValues["BLOOD"].GetString().ShouldBe("B+");
		}
		(await WithUnitOfWorkAsync(async () => (await GetRequiredService<IRepository<AdmissionFormSubmission, Guid>>().GetAsync(fixture.SubmissionId)).SnapshotJson)).ShouldBe(original);
		Console.WriteLine($"Records inline form save SQLite narrow form-ancestry reads={measured.LineageReads}; masked={masked}.");
		// Immutable layout may be fetched once within the save transaction and once
		// again by the fresh authorized response read after that transaction commits.
		measured.LineageReads.ShouldBeLessThanOrEqualTo(2);
	});

	[Fact]
	public Task Records_form_save_failed_mixed_write_does_not_reuse_stale_or_redacted_answers_on_retry() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(structuredCondition: true);
		var service = GetRequiredService<IStudentRecordFormAppService>();
		var snapshot = await service.GetSnapshotAsync(fixture.Student.Id);
		var invalid = InlineFormEdit(snapshot, ("CANONICAL_BLOOD", JsonValue.Create("O+")), ("ZERO", new JsonObject { ["invalid"] = true }));
		(await Should.ThrowAsync<BusinessException>(() => service.SaveAsync(fixture.Student.Id, invalid))).Code.ShouldBe("Students:Records:InvalidCustomValues");
		var unchanged = (await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(fixture.Student.Id)).FormSources.Single();
		unchanged.CurrentValues["CANONICAL_BLOOD"].GetString().ShouldBe("B+");
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentRecordChange, Guid>>().CountAsync(x => x.StudentId == fixture.Student.Id && x.Section == "registration-form"))).ShouldBe(0);
		(await service.SaveAsync(fixture.Student.Id, InlineFormEdit(snapshot, ("CANONICAL_BLOOD", JsonValue.Create("A+"))))).Status.ShouldBe("Applied");
		var current = (await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(fixture.Student.Id)).FormSources.Single();
		current.CurrentValues["CANONICAL_BLOOD"].GetString().ShouldBe("A+");
		current.OriginalValues["CANONICAL_BLOOD"].GetString().ShouldBe("B+");
	});
}
