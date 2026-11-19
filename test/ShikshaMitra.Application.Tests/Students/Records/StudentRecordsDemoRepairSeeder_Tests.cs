using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Seeders;
using ShikshaMitra.Students.Records;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Demo_record_repair_makes_seeded_students_visible_and_is_repeatable() => RunCertificationScenarioAsync(async () =>
	{
		var student = await CreateLegacyDemoStudentAsync();
		var directory = GetRequiredService<IStudentDirectoryAppService>();
		(await directory.GetListAsync(new() { StudentId = student.Id, AcademicYearId = student.AcademicYearId })).TotalCount.ShouldBe(0);
		await RunDemoRecordRepairAsync(useHostContext: true);
		await RunDemoRecordRepairAsync();
		(await directory.GetListAsync(new() { StudentId = student.Id, AcademicYearId = student.AcademicYearId })).TotalCount.ShouldBe(1);
		(await directory.GetListAsync(new() { StudentId = student.Id, AcademicYearId = Guid.NewGuid() })).TotalCount.ShouldBe(0);
		await WithUnitOfWorkAsync(async () =>
		{
			var enrollment = (await _studentEnrollmentRepository.GetListAsync(e => e.StudentId == student.Id)).Single();
			enrollment.DivisionId.ShouldBe(student.DivisionId);
			enrollment.RollNumber.ShouldBe(student.RollNo);
			enrollment.IsActive.ShouldBeTrue();
			var approval = (await GetRequiredService<IRepository<StudentRecordLegacyOverride, Guid>>().GetListAsync(o => o.StudentId == student.Id)).Single();
			approval.Status.ShouldBe("Approved");
			approval.RequestedByUserId.ShouldNotBe(approval.DecidedByUserId!.Value);
			approval.EvidenceReference.ShouldBe("StudentRecordsDemoRepairSeeder:v1");
			(await _onboardingCaseRepository.CountAsync(c => c.StudentId == student.Id)).ShouldBe(0);
		});
	});

	[Fact]
	public Task Demo_record_repair_preserves_existing_enrollment_and_rejected_or_deleted_approvals() => RunCertificationScenarioAsync(async () =>
	{
		var enrolled = await CreateLegacyDemoStudentAsync();
		var rejected = await CreateLegacyDemoStudentAsync();
		var deleted = await CreateLegacyDemoStudentAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			await _studentEnrollmentRepository.InsertAsync(StudentEnrollment.Create(Guid.NewGuid(), TestTenantId,
				enrolled.Id, enrolled.AcademicYearId!.Value, enrolled.StandardId, enrolled.DivisionId, enrolled.RollNo), true);
			var approvals = GetRequiredService<IRepository<StudentRecordLegacyOverride, Guid>>();
			var rejection = new StudentRecordLegacyOverride(Guid.NewGuid(), TestTenantId, rejected.Id, rejected.Id, "Operator rejected this historical import.", null);
			rejection.Decide(TestUserId, false, "Keep rejected", DateTime.UtcNow);
			await approvals.InsertAsync(rejection, true);
			var removed = new StudentRecordLegacyOverride(Guid.NewGuid(), TestTenantId, deleted.Id, deleted.Id, "Operator removed this historical import.", null);
			await approvals.InsertAsync(removed, true);
			await approvals.DeleteAsync(removed, true);
		});
		await RunDemoRecordRepairAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			(await _studentEnrollmentRepository.CountAsync(e => e.StudentId == enrolled.Id)).ShouldBe(1);
			(await _studentEnrollmentRepository.CountAsync(e => e.StudentId == rejected.Id || e.StudentId == deleted.Id)).ShouldBe(0);
			(await GetRequiredService<IRepository<StudentRecordLegacyOverride, Guid>>().CountAsync(o => o.StudentId == enrolled.Id || o.StudentId == deleted.Id)).ShouldBe(0);
			(await GetRequiredService<IRepository<StudentRecordLegacyOverride, Guid>>().GetListAsync(o => o.StudentId == rejected.Id)).Single().Status.ShouldBe("Rejected");
		});
	});

	[Fact]
	public Task Demo_record_repair_ignores_real_accounts_and_minimal_or_wrong_tenant_runs() => RunCertificationScenarioAsync(async () =>
	{
		var demo = await CreateLegacyDemoStudentAsync();
		var real = await CreateLegacyDemoStudentAsync(demoAccount: false);
		await RunDemoRecordRepairAsync(ShikshaMitraSeederProfile.Minimal);
		await WithUnitOfWorkAsync(() => GetRequiredService<StudentRecordsDemoRepairSeederContributor>().SeedAsync(new DataSeedContext(Guid.NewGuid())));
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(e => e.StudentId == demo.Id))).ShouldBe(0);
		await RunDemoRecordRepairAsync(ShikshaMitraSeederProfile.FastDemo);
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(e => e.StudentId == real.Id))).ShouldBe(0);
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(e => e.StudentId == demo.Id))).ShouldBe(1);
	});

	[Fact]
	public Task Demo_record_repair_preserves_occupied_roll_numbers() => RunCertificationScenarioAsync(async () =>
	{
		var student = await CreateLegacyDemoStudentAsync();
		var other = await CreateLegacyDemoStudentAsync(demoAccount: false);
		await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.InsertAsync(StudentEnrollment.Create(Guid.NewGuid(), TestTenantId,
			other.Id, student.AcademicYearId!.Value, student.StandardId, student.DivisionId, student.RollNo), true));
		await RunDemoRecordRepairAsync();
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(e => e.StudentId == student.Id))).ShouldBe(0);
	});

	[Fact]
	public Task Demo_record_repair_does_not_complete_an_existing_onboarding_case() => RunCertificationScenarioAsync(async () =>
	{
		var student = await CreateLegacyDemoStudentAsync();
		var offering = await WithUnitOfWorkAsync(async () => (await _offeringRepository.GetListAsync(o => o.StandardId == student.StandardId)).First());
		var applicant = await CreateCertificationApplicantAsync("DemoProvisioning", true, true, student.Id);
		var application = await StartFixtureApplicationAsync(applicant.Id, offering.Id);
		await WithUnitOfWorkAsync(() => _onboardingCaseRepository.InsertAsync(new Onboarding.StudentOnboardingCase(Guid.NewGuid(), TestTenantId,
			application.Id, applicant.Id, student.Id, offering.Id, student.AcademicYearId!.Value, offering.BoardId,
			offering.MediumId, offering.StageId, offering.StandardId, $"demo-blocker:{student.Id:N}", null, DateTime.UtcNow.AddDays(1)), true));
		await RunDemoRecordRepairAsync();
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(e => e.StudentId == student.Id))).ShouldBe(0);
		(await GetRequiredService<IStudentDirectoryAppService>().GetListAsync(new() { StudentId = student.Id })).TotalCount.ShouldBe(0);
	});

	private Task RunDemoRecordRepairAsync(string profile = ShikshaMitraSeederProfile.FullDemo, bool useHostContext = false) => WithUnitOfWorkAsync(() =>
		GetRequiredService<StudentRecordsDemoRepairSeederContributor>().SeedAsync(new DataSeedContext(useHostContext ? null : TestTenantId)
			.WithProperty(ShikshaMitraSeederProfile.ContextPropertyName, profile)));

	private async Task<Student> CreateLegacyDemoStudentAsync(bool demoAccount = true)
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		return await WithUnitOfWorkAsync(async () =>
		{
			var users = GetRequiredService<IRepository<IdentityUser, Guid>>();
			if (!await users.AnyAsync(u => u.UserName == "admin-ddvs"))
				await users.InsertAsync(new IdentityUser(Guid.NewGuid(), "admin-ddvs", "admin-ddvs@ddvs.com", TestTenantId), true);
			var division = new Division(Guid.NewGuid(), TestTenantId, $"Demo-{Guid.NewGuid():N}"[..20], offering.StandardId, 50, "Demo room");
			await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
			var cycle = await _cycleRepository.GetAsync(offering.AdmissionCycleId);
			var id = Guid.NewGuid();
			var username = $"student-demo-{id:N}";
			await users.InsertAsync(new IdentityUser(id, username, username + (demoAccount ? "@example.invalid" : "@school.example.test"), TestTenantId), true);
			var student = new Student(id, TestTenantId, "DemoRepair", null, "Student", cycle.AcademicYearId,
				offering.BoardId, offering.MediumId, offering.StageId, offering.StandardId, division.Id, 10001, 1,
				StudentLifeCycleStatusEnum.Admission);
			student.LinkUser(id);
			student.ApproveAdmission();
			await _studentRepository.InsertAsync(student, true);
			return student;
		});
	}
}
