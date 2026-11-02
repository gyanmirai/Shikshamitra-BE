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
	public Task Records_hold_all_exit_reuses_current_preflight_inside_its_transaction_and_still_checks_sibling() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await SeedHoldAllReadFixtureAsync();
		var reads = await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			db.ChangeTracker.Clear();
			using var counter = new AcademicQueryCounter(db.Database.GetDbConnection());
			await GetRequiredService<StudentRecordTransitionApplication>().ApplyOrScheduleAsync(fixture.First);
			return counter.ReadCommands.Count(sql => sql.Contains("\"AttendancePlacementHistoryJson\"", StringComparison.Ordinal) &&
				sql.Contains("\"AppStudentEnrollments\"", StringComparison.Ordinal));
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var transitions = GetRequiredService<IRepository<StudentRecordTransition, Guid>>();
			(await transitions.GetAsync(fixture.First)).Status.ShouldBe("Applied");
			(await transitions.GetAsync(fixture.Second)).Status.ShouldBe("Approved");
			(await _studentRepository.GetAsync(fixture.FirstStudent)).Status.ShouldBe(StudentLifeCycleStatusEnum.Left);
			(await _studentRepository.GetAsync(fixture.SecondStudent)).Status.ShouldBe(StudentLifeCycleStatusEnum.Enrolled);
			(await _studentEnrollmentRepository.GetAsync(x => x.StudentId == fixture.FirstStudent)).IsActive.ShouldBeFalse();
			(await _studentEnrollmentRepository.GetAsync(x => x.StudentId == fixture.SecondStudent)).IsActive.ShouldBeTrue();
			(await GetRequiredService<IRepository<StudentRecordTransitionEvent, Guid>>().CountAsync(x => x.TransitionId == fixture.First && x.Action == "Applied")).ShouldBe(1);
		});
		Console.WriteLine($"HoldAll two-row exit: source enrollment materialization reads={reads}.");
		// One current-row preflight, one independent sibling preflight, and one fresh
		// current source at the mutation boundary. The current preflight must not run twice.
		reads.ShouldBe(3);
	});

	[Fact]
	public Task Records_hold_all_exit_rechecks_sibling_after_external_state_change_before_each_transaction() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await SeedHoldAllReadFixtureAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var student = await _studentRepository.GetAsync(fixture.SecondStudent);
			student.ChangeStatus(StudentLifeCycleStatusEnum.Left);
			await _studentRepository.UpdateAsync(student, true);
		});
		await GetRequiredService<StudentRecordTransitionApplication>().ApplyOrScheduleAsync(fixture.First);
		await WithUnitOfWorkAsync(async () =>
		{
			var stored = await GetRequiredService<IRepository<StudentRecordTransition, Guid>>().GetAsync(fixture.First);
			stored.Status.ShouldBe("Exception"); stored.LastError.ShouldBe("Students:Records:BatchHeld");
			(await _studentRepository.GetAsync(fixture.FirstStudent)).Status.ShouldBe(StudentLifeCycleStatusEnum.Enrolled);
			(await _studentEnrollmentRepository.GetAsync(x => x.StudentId == fixture.FirstStudent)).IsActive.ShouldBeTrue();
			(await GetRequiredService<IRepository<StudentRecordTransitionEvent, Guid>>().CountAsync(x => x.TransitionId == fixture.First && x.Action == "Applied")).ShouldBe(0);
		});
	});

	private async Task<(Guid First, Guid Second, Guid FirstStudent, Guid SecondStudent)> SeedHoldAllReadFixtureAsync()
	{
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Status"].Mode = "Direct";
		policy.Transitions.BatchExceptionMode = "HoldAll";
		var fixture = await TransitionFixtureAsync(policy);
		var offering = await WithUnitOfWorkAsync(async () => (await _offeringRepository.GetListAsync(x => x.StandardId == fixture.Student.StandardId)).First());
		var second = await CreateExistingStudentAsync(offering, "HoldAllSecond", "Student");
		await GrantCompletedRecordAsync(second);
		return await WithUnitOfWorkAsync(async () =>
		{
			var batch = new StudentRecordTransitionBatch(Guid.NewGuid(), TestTenantId, fixture.Input.AcademicYearId,
				$"hold-all-read-{Guid.NewGuid():N}", "{}", "[]");
			await GetRequiredService<IRepository<StudentRecordTransitionBatch, Guid>>().InsertAsync(batch, true);
			var ids = new Guid[2]; var students = new[] { fixture.Student.Id, second.Id };
			for (var index = 0; index < students.Length; index++)
			{
				var source = await GetRequiredService<StudentRecordTransitionReadinessService>().GetSourceAsync(students[index], fixture.Input.AcademicYearId);
				var input = new StudentRecordTransitionInput { StudentId = students[index], Outcome = "Transfer",
					EffectiveDate = AdmissionCycleWindowEvaluator.GetInstitutionDate(DateTime.UtcNow), Reason = "Reviewed transfer for preflight certification" };
				var item = new StudentRecordTransition(Guid.NewGuid(), TestTenantId, batch.Id, students[index], fixture.Input.AcademicYearId,
					source.Enrollment.Id, input.Outcome, input.Reason, input.EffectiveDate, JsonSerializer.Serialize(input),
					StudentRecordTransitionReadinessService.Capture(source.Student, source.Enrollment), null, policy.ToJson(),
					JsonSerializer.Serialize(policy.Changes["Status"]), TestUserId, 0);
				await GetRequiredService<IRepository<StudentRecordTransition, Guid>>().InsertAsync(item, true);
				ids[index] = item.Id;
			}
			return (ids[0], ids[1], students[0], students[1]);
		});
	}
}
