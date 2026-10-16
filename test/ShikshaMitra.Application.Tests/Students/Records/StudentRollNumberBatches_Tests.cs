using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using ShikshaMitra.Security.UserScope;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	private async Task<(Student Student, StudentRollScopeInput Scope)> RollBatchFixtureAsync(string grouping = "mixed", bool approval = false)
	{
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Placement"].Mode = approval ? "Approval" : "Direct";
		var fixture = await TransitionFixtureAsync(policy);
		var standard = fixture.Student.StandardId!.Value;
		var division = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<Division, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId, "RollBatchA", standard, 100, ""), true));
		var scope = new StudentRollScopeInput { AcademicYearId = fixture.Student.AcademicYearId!.Value, StandardId = standard, DivisionId = division.Id };
		await WithUnitOfWorkAsync(async () => {
			var enrollment = await _studentEnrollmentRepository.GetAsync(x => x.StudentId == fixture.Student.Id && x.IsActive);
			enrollment.ConfirmAttendanceStart(DateTime.UtcNow.Date.AddDays(-3));
			await _studentEnrollmentRepository.UpdateAsync(enrollment, true);
		});
		await PlaceRollBatchStudentAsync(fixture.Student.Id, scope, null);
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var saved = (await service.GetSettingsAsync(scope.AcademicYearId)).SingleOrDefault(x => x.AcademicYearId == scope.AcademicYearId);
		var configuration = JsonNode.Parse(saved?.ConfigurationJson ?? JsonSerializer.Serialize(new {
			readinessAccess = new { }, onboardingProfiles = Array.Empty<object>(), grNumber = new { configurations = Array.Empty<object>() }, divisionChange = new { configurations = Array.Empty<object>() },
			rollNumber = new { assignmentMode = "both", configurations = Array.Empty<object>() } }))!.AsObject();
		var rule = JsonSerializer.SerializeToNode(new { id = "batch-rule", status = "draft", version = 1, isActive = false, boardMode = "all", mediumMode = "all", stageMode = "all", classMode = "selected", standardIds = new[] { standard }, divisionMode = "all", grouping,
			nameFormat = "first-name-first", arrangeBy = "studentName", arrangeType = "ascending", startingNumber = 1, numberingScope = "class-division", preserveExisting = true, midYearHandling = "next-available" })!;
		configuration["rollNumber"]!["assignmentMode"] = "both";
		configuration["rollNumber"]!["configurations"]!.AsArray().Add(rule);
		saved = await service.SaveSettingsAsync(new() { AcademicYearId = scope.AcademicYearId, ConfigurationJson = configuration.ToJsonString(), ConcurrencyStamp = saved?.ConcurrencyStamp });
		rule["status"] = "published"; rule["isActive"] = true;
		await service.SaveSettingsAsync(new() { AcademicYearId = scope.AcademicYearId, ConfigurationJson = configuration.ToJsonString(), ConcurrencyStamp = saved.ConcurrencyStamp });
		return (fixture.Student, scope);
	}
	private async Task PlaceRollBatchStudentAsync(Guid id, StudentRollScopeInput scope, int? roll)
	{
		await WithUnitOfWorkAsync(async () => {
			var student = await _studentRepository.GetAsync(id);
			student.AssignDivision(scope.DivisionId); student.AssignRollNo(roll); await _studentRepository.UpdateAsync(student, true);
			var enrollment = await _studentEnrollmentRepository.GetAsync(x => x.StudentId == id && x.IsActive);
			enrollment.ChangePlacement(scope.StandardId, scope.DivisionId, roll, DateTime.UtcNow.Date.AddDays(-2));
			await _studentEnrollmentRepository.UpdateAsync(enrollment, true);
		});
	}
	private Task<StudentRollNumberBatchDto> RollBatchPreviewAsync(StudentRollScopeInput scope, string operation = "assign-missing") =>
		GetRequiredService<IStudentRollNumberBatchAppService>().PreviewAsync(new() { AcademicYearId = scope.AcademicYearId, StandardId = scope.StandardId, DivisionId = scope.DivisionId, Operation = operation });
	private static SubmitStudentRollBatchInput RollBatchSubmit(StudentRollNumberBatchDto preview, string reason = "Reviewed division roster") => new() { ConcurrencyStamp = preview.ConcurrencyStamp, IdempotencyKey = preview.Id.ToString("D"), Reason = reason };

	[Fact]
	public Task Roll_batch_assigns_full_server_cohort_and_does_not_reuse_the_departed_highest_number() => RunCertificationScenarioAsync(async () => {
		var fixture = await RollBatchFixtureAsync(); var service = GetRequiredService<IStudentRollNumberBatchAppService>();
		var first = await RollBatchPreviewAsync(fixture.Scope); first.TotalCount.ShouldBe(1); first.Items.Single().ProposedRollNumber.ShouldBe(1);
		var input = RollBatchSubmit(first); (await service.SubmitAsync(first.Id, input)).Status.ShouldBe("Applied");
		(await service.SubmitAsync(first.Id, input)).Status.ShouldBe("Applied");
		await WithUnitOfWorkAsync(async () => { var enrollment = await _studentEnrollmentRepository.GetAsync(x => x.StudentId == fixture.Student.Id && x.IsActive); enrollment.Deactivate(DateTime.UtcNow.Date); await _studentEnrollmentRepository.UpdateAsync(enrollment, true); });
		var offering = await WithUnitOfWorkAsync(async () => (await _offeringRepository.GetListAsync(x => x.StandardId == fixture.Scope.StandardId)).First());
		var late = await CreateExistingStudentAsync(offering, "AaronLate", "Shah"); await GrantCompletedRecordAsync(late); await PlaceRollBatchStudentAsync(late.Id, fixture.Scope, null);
		var other = await CreateExistingStudentAsync(offering, "ZoeLate", "Shah"); await GrantCompletedRecordAsync(other); await PlaceRollBatchStudentAsync(other.Id, fixture.Scope, null);
		var next = await RollBatchPreviewAsync(fixture.Scope); next.TotalCount.ShouldBe(2); next.Items.First().ProposedRollNumber.ShouldBe(2);
		var page = await service.GetAsync(next.Id, new PagedResultRequestDto { SkipCount = 1, MaxResultCount = 1 });
		page.TotalCount.ShouldBe(2); page.Items.Single().StudentId.ShouldBe(other.Id); page.Items.Single().ProposedRollNumber.ShouldBe(3);
		(await service.SubmitAsync(next.Id, RollBatchSubmit(next))).Status.ShouldBe("Applied");
		(await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(late.Id))).RollNo.ShouldBe(2);
		(await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(other.Id))).RollNo.ShouldBe(3);
		(await service.GetContextAsync(fixture.Scope)).UnassignedCount.ShouldBe(0);
	});
	[Fact]
	public Task Roll_batch_rejects_changed_roster_without_partially_assigning_any_student() => RunCertificationScenarioAsync(async () => {
		var fixture = await RollBatchFixtureAsync(); var preview = await RollBatchPreviewAsync(fixture.Scope);
		await WithUnitOfWorkAsync(async () => { var student = await _studentRepository.GetAsync(fixture.Student.Id); student.SetIdentifierLocked("roll", true); await _studentRepository.UpdateAsync(student, true); });
		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IStudentRollNumberBatchAppService>().SubmitAsync(preview.Id, RollBatchSubmit(preview)))).Code.ShouldBe(StudentRollNumberErrorCodes.StalePreview);
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(x => x.StudentId == fixture.Student.Id && x.IsActive))).RollNumber.ShouldBeNull();
	});
	[Fact]
	public Task Roll_batch_renumber_swaps_numbers_atomically_and_preserves_previous_dated_roster() => RunCertificationScenarioAsync(async () => {
		var fixture = await RollBatchFixtureAsync(); await PlaceRollBatchStudentAsync(fixture.Student.Id, fixture.Scope, 1);
		var offering = await WithUnitOfWorkAsync(async () => (await _offeringRepository.GetListAsync(x => x.StandardId == fixture.Scope.StandardId)).First());
		var aaron = await CreateExistingStudentAsync(offering, "Aaron", "Shah"); await GrantCompletedRecordAsync(aaron); await PlaceRollBatchStudentAsync(aaron.Id, fixture.Scope, 2);
		var preview = await RollBatchPreviewAsync(fixture.Scope, "renumber"); preview.TotalCount.ShouldBe(2); preview.Items.First().StudentId.ShouldBe(aaron.Id);
		(await GetRequiredService<IStudentRollNumberBatchAppService>().SubmitAsync(preview.Id, RollBatchSubmit(preview))).Status.ShouldBe("Applied");
		var previous = await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(x => x.StudentId == fixture.Student.Id && x.IsActive));
		previous.RollNumber.ShouldBe(2); previous.AttendancePlacementOn(DateTime.UtcNow.Date.AddDays(-1))!.RollNumber.ShouldBe(1);
		(await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(aaron.Id))).RollNo.ShouldBe(1);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentRecordChange, Guid>>().CountAsync(x => x.StudentId == aaron.Id && x.Action == "Placement"))).ShouldBe(1);
	});
	[Fact]
	public Task Roll_batch_approval_is_one_independent_review_for_the_whole_division() => RunCertificationScenarioAsync(async () => {
		var fixture = await RollBatchFixtureAsync(approval: true); var service = GetRequiredService<IStudentRollNumberBatchAppService>();
		var preview = await RollBatchPreviewAsync(fixture.Scope); var input = RollBatchSubmit(preview);
		var pending = await service.SubmitAsync(preview.Id, input); pending.Status.ShouldBe("PendingApproval");
		(await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(fixture.Student.Id))).RollNo.ShouldBeNull();
		(await Should.ThrowAsync<BusinessException>(() => service.DecideAsync(pending.Id, new() { ConcurrencyStamp = pending.ConcurrencyStamp, Approve = true }))).Code.ShouldBe(StudentRollNumberErrorCodes.ApproverRequired);
		var reviewer = Guid.NewGuid(); await EnsureTestIdentityUserAsync(reviewer, "roll-reviewer");
		await WithUnitOfWorkAsync(async () => { var users = GetRequiredService<IdentityUserManager>(); var actor = (await users.FindByIdAsync(reviewer.ToString()))!; (await users.AddToRoleAsync(actor, "admin")).Succeeded.ShouldBeTrue();
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: reviewer, description: "Division reviewer"), true); });
		using (_principalAccessor.Change(ProfilePrincipal(reviewer))) pending = await service.DecideAsync(pending.Id, new() { ConcurrencyStamp = pending.ConcurrencyStamp, Approve = true, Reason = "Reviewed roster" });
		pending.Status.ShouldBe("Approved");
		input.ConcurrencyStamp = pending.ConcurrencyStamp; (await service.ApplyAsync(pending.Id, input)).Status.ShouldBe("Applied");
		(await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(fixture.Student.Id))).RollNo.ShouldBe(1);
	});
	[Fact]
	public Task Roll_batch_renumber_allows_an_authorized_scoped_operator_without_admin_membership() => RunCertificationScenarioAsync(async () => {
		var fixture = await RollBatchFixtureAsync(); var operatorId = Guid.NewGuid(); await EnsureTestIdentityUserAsync(operatorId, "roll-operator");
		await WithUnitOfWorkAsync(() => _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: operatorId, description: "Full scoped operator"), true));
		using (_principalAccessor.Change(ProfilePrincipal(operatorId)))
			(await RollBatchPreviewAsync(fixture.Scope, "renumber")).Operation.ShouldBe("renumber");
	});
	[Fact]
	public Task Roll_batch_read_is_tenant_scoped_and_page_size_is_bounded() => RunCertificationScenarioAsync(async () => {
		var fixture = await RollBatchFixtureAsync(); var preview = await RollBatchPreviewAsync(fixture.Scope); var service = GetRequiredService<IStudentRollNumberBatchAppService>();
		await Should.ThrowAsync<BusinessException>(() => service.GetAsync(preview.Id, new PagedResultRequestDto { MaxResultCount = 101 }));
		using (_currentTenant.Change(Guid.NewGuid())) await Should.ThrowAsync<Exception>(() => service.GetAsync(preview.Id, new PagedResultRequestDto { MaxResultCount = 25 }));
	});
}
