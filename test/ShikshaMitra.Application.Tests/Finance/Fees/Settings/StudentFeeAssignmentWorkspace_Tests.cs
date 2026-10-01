using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
	private async Task<AssignStudentFeeStructureDto> ReadyAssignmentAsync()
	{
		var (metadataId, standardId, candidate) = await ReadyCandidateAsync();
		candidate.ConcessionPolicyId = null;
		candidate.ConcessionPolicyVersionId = null;
		var draft = await _feeStructures.CreateAsync(metadataId, new() { StandardId = standardId, StandardFeeStructures = candidate });
		var structure = await _feeStructures.PublishAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp });
		var metadata = await _metadataRepo.GetAsync(metadataId);
		var standard = await _standardRepo.GetAsync(standardId);
		var student = new Student(Guid.NewGuid(), TestTenantId, "Assignment", null, "Student", metadata.AcademicYearId!.Value,
			metadata.AcademicBoardId!.Value, metadata.AcademicMediumId!.Value, standard.EducationMediumStageId, standardId, status: StudentLifeCycleStatusEnum.Enrolled);
		await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, autoSave: true);

		return new() { StudentId = student.Id, AcademicYearId = metadata.AcademicYearId.Value, StandardFeeStructureId = structure.Id };
	}

	[Fact]
	public async Task Assignment_Bulk_Commits_All_Six_Previews_For_The_Same_Structure_And_Retry_Is_Idempotent()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var first = await ReadyAssignmentAsync();
		var students = GetRequiredService<IRepository<Student, Guid>>();
		var original = await students.GetAsync(first.StudentId);
		var items = new System.Collections.Generic.List<AssignStudentFeeStructureDto> { first };
		for (var i = 1; i < 6; i++)
		{
			var student = new Student(Guid.NewGuid(), TestTenantId, $"Assignment {i}", null, "Student", first.AcademicYearId,
				original.AcademicBoardId!.Value, original.AcademicMediumId, original.AcademicMediumStageId!.Value,
				original.StandardId, status: StudentLifeCycleStatusEnum.Enrolled);
			await students.InsertAsync(student, autoSave: true);
			items.Add(new() { StudentId = student.Id, AcademicYearId = first.AcademicYearId, StandardFeeStructureId = first.StandardFeeStructureId });
		}
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var preview = await service.PreviewBulkAsync(new() { Items = items });
		preview.Results.Count.ShouldBe(6);
		foreach (var item in items)
			item.PreviewToken = preview.Results.Single(r => r.StudentId == item.StudentId).Preview.ShouldNotBeNull().PreviewToken;
		var uowManager = GetRequiredService<IUnitOfWorkManager>();
		var structures = GetRequiredService<IRepository<FeeStructure, Guid>>();
		string structureRevision;
		string yearRevision;
		using (var verification = uowManager.Begin(requiresNew: true, isTransactional: false))
		{
			structureRevision = (await structures.GetAsync(first.StandardFeeStructureId)).ConcurrencyStamp;
			yearRevision = (await _academicYearRepo.GetAsync(first.AcademicYearId)).ConcurrencyStamp;
		}

		var result = await service.BulkAssignAsync(new() { Items = items });
		result.Results.ShouldAllBe(r => r.Outcome == "Assigned", string.Join("; ", result.Results.Select(r => $"{r.StudentId}: {r.ErrorMessage}")));
		result.SuccessCount.ShouldBe(6);
		result.FailedCount.ShouldBe(0);
		using (var verification = uowManager.Begin(requiresNew: true, isTransactional: false))
		{
			(await structures.GetAsync(first.StandardFeeStructureId)).ConcurrencyStamp.ShouldBe(structureRevision);
			(await _academicYearRepo.GetAsync(first.AcademicYearId)).ConcurrencyStamp.ShouldBe(yearRevision);
		}
		var dues = GetRequiredService<IRepository<DueFee, Guid>>();
		foreach (var item in items)
		{
			var committed = await dues.GetListAsync(d => d.StudentId == item.StudentId);
			committed.Count.ShouldBe(2);
			committed.Sum(d => d.OriginalAmount).ShouldBe(1000.01m);
			committed.ShouldAllBe(d => d.StudentFeeStructureId == result.Results.Single(r => r.StudentId == item.StudentId).Result!.AssignmentId);
		}
		var retry = await service.BulkAssignAsync(new() { Items = items });
		retry.AlreadyAssignedCount.ShouldBe(6);
		retry.FailedCount.ShouldBe(0);
		foreach (var item in items)
			(await dues.GetListAsync(d => d.StudentId == item.StudentId)).Count.ShouldBe(2);
	}

	[Fact]
	public async Task Assignment_Changed_Academic_Year_Invalidates_Preview_Without_Writes()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var input = await ReadyAssignmentAsync();
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		input.PreviewToken = (await service.PreviewAsync(input)).PreviewToken;
		using (var change = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			var year = await _academicYearRepo.GetAsync(input.AcademicYearId);
			year.SetDates(year.StartDate.AddDays(1), year.EndDate);
			await _academicYearRepo.UpdateAsync(year);
			await change.CompleteAsync();
		}
		(await Should.ThrowAsync<BusinessException>(() => service.AssignAsync(input))).Code.ShouldBe(AssignmentErrorCodes.StalePreview);
		(await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().GetListAsync(x => x.StudentId == input.StudentId)).ShouldBeEmpty();
		(await GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(x => x.StudentId == input.StudentId)).ShouldBeEmpty();
	}

	[Fact]
	public async Task Assignment_Preview_Commit_Retry_And_Read_Work_Without_Concession()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var input = await ReadyAssignmentAsync();
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var dues = GetRequiredService<IRepository<DueFee, Guid>>();
		var preview = await service.PreviewAsync(input);
		(await dues.GetListAsync(d => d.StudentId == input.StudentId)).ShouldBeEmpty();
		preview.ProratedTotal.ShouldBe(1000.01m);
		preview.Policies.ShouldNotContain(p => p.Family == "concession");
		input.PreviewToken = preview.PreviewToken;
		var result = await service.AssignAsync(input);
		result.Outcome.ShouldBe("Assigned");
		(await service.AssignAsync(input)).Outcome.ShouldBe("AlreadyAssigned");
		var committed = await dues.GetListAsync(d => d.StudentFeeStructureId == result.AssignmentId);
		committed.Count.ShouldBe(2);
		committed.Sum(d => d.OriginalAmount).ShouldBe(1000.01m);
		var read = await service.GetByStudentIdAsync(input.StudentId, input.AcademicYearId);
		read.ConcessionPolicyId.ShouldBeNull();
		read.Assignment!.Lines.Count.ShouldBe(2);
		read.FeeHeads.Count.ShouldBe(1);
		committed[0].ApplyPayment(100m);
		await dues.UpdateAsync(committed[0], autoSave: true);
		var paidRetry = await service.AssignAsync(input);
		paidRetry.Outcome.ShouldBe("AlreadyAssigned");
		paidRetry.Assignment.Lines.Sum(l => l.PaidAmount).ShouldBe(100m);
		paidRetry.Assignment.Lines.Sum(l => l.OutstandingAmount).ShouldBe(900.01m);
	}

	[Fact]
	public async Task Assignment_Tampered_Preview_Leaves_No_Partial_Record()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var input = await ReadyAssignmentAsync();
		input.PreviewToken = "tampered";
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var error = await Should.ThrowAsync<BusinessException>(() => service.AssignAsync(input));
		error.Code.ShouldBe(AssignmentErrorCodes.StalePreview);
		(await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().GetListAsync(x => x.StudentId == input.StudentId)).ShouldBeEmpty();
		(await GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(x => x.StudentId == input.StudentId)).ShouldBeEmpty();
	}

	[Fact]
	public async Task Assignment_Rejects_Caller_Dates_Labelled_As_Verified_Admission()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var input = await ReadyAssignmentAsync();
		input.ChargePeriodSource = "Admission";
		var error = await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IStudentFeeStructureAppService>().PreviewAsync(input));
		error.Code.ShouldBe(AssignmentErrorCodes.SourceUnavailable);
	}

	[Fact]
	public async Task Assignment_Changed_Amount_Invalidates_Preview_And_Bulk_Preserves_Valid_Student()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var input = await ReadyAssignmentAsync();
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var preview = await service.PreviewAsync(input);
		input.PreviewToken = preview.PreviewToken;
		input.OverrideReason = "Approved annual amount change";
		input.FeeHeadDetails.Add(new() { FeeHeadId = preview.Heads.Single().FeeHeadId, Amount = 800m });
		(await Should.ThrowAsync<BusinessException>(() => service.AssignAsync(input))).Code.ShouldBe(AssignmentErrorCodes.StalePreview);
		var refreshed = await service.PreviewAsync(input);
		refreshed.ProratedTotal.ShouldBe(800m);
		input.PreviewToken = refreshed.PreviewToken;
		var invalid = new AssignStudentFeeStructureDto { StudentId = Guid.NewGuid(), AcademicYearId = input.AcademicYearId, StandardFeeStructureId = input.StandardFeeStructureId };
		var result = await service.BulkAssignAsync(new() { Items = [invalid, input] });
		result.SuccessCount.ShouldBe(1);
		result.FailedCount.ShouldBe(1);
		result.Results.Single(r => r.StudentId == input.StudentId).Result!.Assignment.ProratedTotal.ShouldBe(800m);
		var retry = await service.BulkAssignAsync(new() { Items = [input] });
		retry.AlreadyAssignedCount.ShouldBe(1);
		(await GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(d => d.StudentId == input.StudentId)).Sum(d => d.OriginalAmount).ShouldBe(800m);
	}

	[Fact]
	public async Task Assignment_Read_Uses_Selected_Year_After_Student_Promotion()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var input = await ReadyAssignmentAsync();
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		input.PreviewToken = (await service.PreviewAsync(input)).PreviewToken;
		var result = await service.AssignAsync(input);
		var students = GetRequiredService<IRepository<Student, Guid>>();
		var student = await students.GetAsync(input.StudentId);
		var nextYear = new ShikshaMitra.TenantSettings.AcademicYears.AcademicYear(Guid.NewGuid(), TestTenantId, "2026-27", new(2026, 4, 1), new(2027, 3, 31));
		await _academicYearRepo.InsertAsync(nextYear, autoSave: true);
		student.UpdateAcademicPlacement(nextYear.Id, student.AcademicBoardId!.Value, student.AcademicMediumId, student.AcademicMediumStageId!.Value, student.StandardId, student.DivisionId);
		await students.UpdateAsync(student, autoSave: true);
		var historical = await service.GetByStudentIdAsync(input.StudentId, input.AcademicYearId);
		historical.StudentFeeStructureId.ShouldBe(result.AssignmentId);
		historical.Assignment!.ProratedTotal.ShouldBe(1000.01m);
		var list = await service.GetStudentsAsync(new() { AcademicYearId = input.AcademicYearId, MaxResultCount = 10 });
		list.Items.Single(s => s.StudentId == input.StudentId).IsFeeStructureAssigned.ShouldBeTrue();
	}

	[Fact]
	public async Task Assignment_Class_Scope_Does_Not_Grant_Sibling_Classes_Or_Other_Tenants()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var input = await ReadyAssignmentAsync();
		var students = GetRequiredService<IRepository<Student, Guid>>();
		var original = await students.GetAsync(input.StudentId);
		var siblingClass = new ShikshaMitra.TenantSettings.Standards.Standard(Guid.NewGuid(), TestTenantId, "Sibling class", 5, original.AcademicMediumStageId!.Value, null);
		await _standardRepo.InsertAsync(siblingClass, autoSave: true);
		var sibling = new Student(Guid.NewGuid(), TestTenantId, "Other", null, "Class", input.AcademicYearId, original.AcademicBoardId!.Value,
			original.AcademicMediumId, original.AcademicMediumStageId.Value, siblingClass.Id, status: StudentLifeCycleStatusEnum.Enrolled);
		await students.InsertAsync(sibling, autoSave: true);
		var limitedId = Guid.NewGuid();
		await GetRequiredService<IRepository<Volo.Abp.Identity.IdentityUser, Guid>>().InsertAsync(new Volo.Abp.Identity.IdentityUser(limitedId, "class-reviewer", "class-reviewer@example.test", TestTenantId), autoSave: true);
		await GetRequiredService<IRepository<ShikshaMitra.Security.UserScope.UserScopeAssignment, Guid>>().InsertAsync(new ShikshaMitra.Security.UserScope.UserScopeAssignment(Guid.NewGuid(), TestTenantId,
			userId: limitedId, academicYearId: input.AcademicYearId, educationBoardId: original.AcademicBoardId, educationMediumId: original.AcademicMediumId,
			educationMediumStageId: original.AcademicMediumStageId, standardId: original.StandardId), autoSave: true);
		using var limited = GetRequiredService<Volo.Abp.Security.Claims.ICurrentPrincipalAccessor>().Change(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
			[new System.Security.Claims.Claim(Volo.Abp.Security.Claims.AbpClaimTypes.UserId, limitedId.ToString())], "Test")));
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var list = await service.GetStudentsAsync(new() { AcademicYearId = input.AcademicYearId, MaxResultCount = 10 });
		list.Items.Select(x => x.StudentId).ShouldBe(new[] { input.StudentId });
		(await Should.ThrowAsync<BusinessException>(() => service.GetCandidatesAsync(sibling.Id, input.AcademicYearId))).Code.ShouldBe(AssignmentErrorCodes.EnrollmentRequired);
		using var otherTenant = _currentTenant.Change(Guid.NewGuid());
		(await Should.ThrowAsync<BusinessException>(() => service.GetCandidatesAsync(input.StudentId, input.AcademicYearId))).Code.ShouldBe(AssignmentErrorCodes.EnrollmentRequired);
	}
}
