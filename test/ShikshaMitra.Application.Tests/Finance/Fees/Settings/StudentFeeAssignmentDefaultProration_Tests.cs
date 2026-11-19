using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
	private async Task<(AssignStudentFeeStructureDto Input, ProrationPolicyDto Policy, Guid HeadId)> DefaultProrationAssignmentAsync(
		DateTime? serviceEnd = null)
	{
		var input = await ReadyAssignmentAsync(amount: 12000m);
		var structure = await GetRequiredService<IRepository<FeeStructure, Guid>>().GetAsync(input.StandardFeeStructureId);
		var headId = structure.FeeStructureDetails.Single().FeeHeadId;
		var policy = await GetRequiredService<IProrationPolicyAppService>().CreateAsync(new()
		{
			AcademicYearId = input.AcademicYearId, PolicyName = "Automatic default proration", Status = "Active", IsDefault = true,
			FeeHeadRules = [new() { FeeHeadId = headId, RuleType = "Daily", ApplyOnAdmission = true,
				ApplyOnEarlyWithdrawal = true, EarlyWithdrawalRuleType = "Daily" }],
		});
		policy = await GetRequiredService<IProrationPolicyAppService>().GetAsync(policy.Id);
		input.ServicePeriods = [new() { FeeHeadId = headId, Source = "ManualOverride", StartDate = new(2025, 10, 1),
			EndDate = serviceEnd ?? new(2026, 3, 31), Reason = "Confirmed service period" }];
		return (input, policy, headId);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Default_Proration_Auto_Attaches_In_Both_Preparation_Flows(bool admissionFlow)
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var (input, policy, _) = await DefaultProrationAssignmentAsync();
		var service = GetRequiredService<StudentFeeStructureAppService>();
		StudentFeeAssignmentPreviewDto preview;
		if (admissionFlow)
		{
			var student = await GetRequiredService<IRepository<Student, Guid>>().GetAsync(input.StudentId);
			var structure = await GetRequiredService<IRepository<FeeStructure, Guid>>().GetAsync(input.StandardFeeStructureId);
			var source = new AdmissionFinanceContext(Guid.NewGuid(), Guid.NewGuid(), input.AcademicYearId,
				student.AcademicBoardId!.Value, student.AcademicMediumId, student.AcademicMediumStageId!.Value,
				student.StandardId!.Value, null, student.Id, "Default Student", "DEFAULT-APP", "Class", "revision",
				structure.Id, structure.RevisionNumber, [], true, null, student.Id, new(2025, 10, 1));
			var prepared = await WithUnitOfWorkAsync(() => service.PrepareAdmissionAsync(source, Guid.NewGuid(), []));
			preview = prepared.Preview;
			AdmissionFeeCollectionAppService.ProrationResult(preview, prepared.Dues)!.JoiningDate.ShouldBe(new DateTime(2025, 10, 1));
		}
		else preview = await service.PreviewAsync(input);
		preview.ProrationPolicyVersionId.ShouldBe(policy.CurrentVersionId);
		preview.Policies.Single(x => x.Family == "proration").Source.ShouldBe("FinanceDefault");
		preview.ProratedTotal.ShouldBe(5984m);
		preview.Lines.Count.ShouldBe(1);
		preview.Lines.Single().RuleType.ShouldBe("Daily");
		if (!admissionFlow)
		{
			input.PreviewToken = preview.PreviewToken;
			var result = await service.AssignAsync(input);
			var saved = await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().GetAsync(result.AssignmentId);
			saved.ProrationPolicyVersionId.ShouldBe(policy.CurrentVersionId);
			(await WithUnitOfWorkAsync(() => service.GetByStudentIdAsync(input.StudentId, input.AcademicYearId))).Assignment!.ProratedTotal.ShouldBe(5984m);
		}
	}

	[Fact]
	public async Task Default_Proration_Bulk_Attaches_With_Each_Students_Own_Service_Dates()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var (input, policy, headId) = await DefaultProrationAssignmentAsync();
		var students = GetRequiredService<IRepository<Student, Guid>>();
		var first = await students.GetAsync(input.StudentId);
		var second = new Student(Guid.NewGuid(), TestTenantId, "Second default", null, "Student", input.AcademicYearId,
			first.AcademicBoardId!.Value, first.AcademicMediumId, first.AcademicMediumStageId!.Value, first.StandardId,
			status: StudentLifeCycleStatusEnum.Enrolled);
		await students.InsertAsync(second, autoSave: true);
		var secondInput = new AssignStudentFeeStructureDto { StudentId = second.Id, AcademicYearId = input.AcademicYearId,
			StandardFeeStructureId = input.StandardFeeStructureId, ServicePeriods = [new() { FeeHeadId = headId,
				Source = "ManualOverride", StartDate = new(2025, 11, 1), EndDate = new(2026, 3, 31), Reason = "Second service period" }] };
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var previews = await service.PreviewBulkAsync(new() { Items = [input, secondInput] });
		input.PreviewToken = previews.Results.Single(x => x.StudentId == input.StudentId).Preview!.PreviewToken;
		secondInput.PreviewToken = previews.Results.Single(x => x.StudentId == second.Id).Preview!.PreviewToken;
		var result = await service.BulkAssignAsync(new() { Items = [input, secondInput] });
		result.SuccessCount.ShouldBe(2);
		result.Results.ShouldAllBe(x => x.Result!.Assignment.ProrationPolicyVersionId == policy.CurrentVersionId);
		result.Results.Single(x => x.StudentId == input.StudentId).Result!.Assignment.ProratedTotal.ShouldBe(5984m);
		result.Results.Single(x => x.StudentId == second.Id).Result!.Assignment.ProratedTotal.ShouldBe(4965m);
		(await service.BulkAssignAsync(new() { Items = [input, secondInput] })).AlreadyAssignedCount.ShouldBe(2);
	}

	[Fact]
	public async Task Default_Proration_Is_Advertised_For_Students_Without_An_Admission_Link()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var (input, policy, _) = await DefaultProrationAssignmentAsync();
		var contexts = await GetRequiredService<IStudentFeeStructureAppService>().GetContextsAsync(new()
			{ AcademicYearId = input.AcademicYearId, StudentIds = [input.StudentId] });
		var context = contexts.Single();
		context.ProrationPolicy.ShouldNotBeNull().VersionId.ShouldBe(policy.CurrentVersionId!.Value);
		context.ProrationPolicy.Source.ShouldBe("FinanceDefault");
		context.JoiningDate.ShouldBeNull(); // Attaching a policy must not invent a joining date.
	}

	[Fact]
	public async Task Default_Proration_Attaches_To_A_Full_Year_Without_Inventing_Admission_Dates()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var (input, policy, _) = await DefaultProrationAssignmentAsync();
		input.ServicePeriods = [];
		var preview = await GetRequiredService<IStudentFeeStructureAppService>().PreviewAsync(input);
		preview.ProrationPolicyVersionId.ShouldBe(policy.CurrentVersionId);
		preview.ProratedTotal.ShouldBe(12000m);
		preview.RegistrationJoiningDate.ShouldBeNull();
	}

	[Fact]
	public async Task Default_Proration_Version_Changes_Require_A_Fresh_Preview_And_Preserve_Saved_History()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var (input, policy, headId) = await DefaultProrationAssignmentAsync();
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		input.PreviewToken = (await service.PreviewAsync(input)).PreviewToken;
		var policies = GetRequiredService<IProrationPolicyAppService>();
		await policies.UpdateAsync(policy.Id, new() { AcademicYearId = input.AcademicYearId, PolicyName = policy.PolicyName,
			PolicyCode = policy.PolicyCode, Status = "Active", IsDefault = true, ConcurrencyStamp = policy.ConcurrencyStamp,
			ChangeReason = "New assignments use whole service months", FeeHeadRules = [new() { FeeHeadId = headId,
				RuleType = "FullMonthCharge", ApplyOnAdmission = true, ApplyOnEarlyWithdrawal = true, EarlyWithdrawalRuleType = "Daily" }] });
		(await Should.ThrowAsync<BusinessException>(() => service.AssignAsync(input))).Code.ShouldBe(AssignmentErrorCodes.StalePreview);
		var preview = await service.PreviewAsync(input);
		preview.ProratedTotal.ShouldBe(6000m);
		input.PreviewToken = preview.PreviewToken;
		var saved = await service.AssignAsync(input);
		policy = await policies.GetAsync(policy.Id);
		await policies.UpdateAsync(policy.Id, new() { AcademicYearId = input.AcademicYearId, PolicyName = policy.PolicyName,
			PolicyCode = policy.PolicyCode, Status = "Active", IsDefault = true, ConcurrencyStamp = policy.ConcurrencyStamp,
			ChangeReason = "Future assignments use daily calculation", FeeHeadRules = [new() { FeeHeadId = headId,
				RuleType = "Daily", ApplyOnAdmission = true, ApplyOnEarlyWithdrawal = true, EarlyWithdrawalRuleType = "Daily" }] });
		var retry = await service.AssignAsync(input);
		retry.AssignmentId.ShouldBe(saved.AssignmentId);
		retry.Assignment.ProrationPolicyVersionId.ShouldBe(preview.ProrationPolicyVersionId);
		retry.Assignment.ProratedTotal.ShouldBe(6000m);
	}

	[Theory]
	[InlineData("2026-03-31", "2025-12-31", 5984, 2959)]
	[InlineData("2026-03-31", "2025-09-30", 5984, 5984)]
	[InlineData("2025-12-31", "2026-01-15", 3025, 0)]
	public async Task Default_Proration_Withdrawal_Uses_The_Saved_Head_Service_Period(string serviceEnd, string leaving,
		decimal original, decimal expectedCredit)
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var (input, _, _) = await DefaultProrationAssignmentAsync(DateTime.Parse(serviceEnd));
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var preview = await service.PreviewAsync(input);
		preview.ProratedTotal.ShouldBe(original);
		input.PreviewToken = preview.PreviewToken;
		await service.AssignAsync(input);
		var withdrawal = await WithUnitOfWorkAsync(() => GetRequiredService<WithdrawalProrationService>().PreviewAsync(input.StudentId,
			input.AcademicYearId, DateTime.Parse(leaving)));
		withdrawal.Lines.Sum(x => x.Credit).ShouldBe(expectedCredit);
		if (expectedCredit > 0m) withdrawal.Result!.JoiningDate.ShouldBeNull();
		(await GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(x => x.StudentId == input.StudentId))
			.Sum(x => x.OriginalAmount).ShouldBe(original); // Preview is read-only.
	}
}
