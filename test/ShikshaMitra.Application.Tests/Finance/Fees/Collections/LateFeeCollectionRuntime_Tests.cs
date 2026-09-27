using Shouldly;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

/// <summary>
/// Integration tests for Phase 3 (Fee Collection runtime integration) — verifies late fee is
/// computed at the point of collection using the same LateFeeCalculationService as /simulate,
/// waivers net out of the displayed/collectible amount, and the collect endpoint validates
/// (not just trusts) the late fee amount a client submits.
///
/// TestTenantId and FakeAdminUserId are pinned to FakeCurrentPrincipalAccessor's claims
/// (ShikshaMitra.TestBase/Security/FakeCurrentPrincipalAccessor.cs) — GetCollectFormAsync and
/// CollectFeeAsync both go through FinanceScopeAccessService.EnsureStudentAccessibleAsync,
/// which resolves the *current user's* admin-role membership from the database, not from the
/// fake principal's role claim. Without a real IdentityUser/IdentityRole row matching that
/// principal, every scope-gated call fails with "student is outside your user scope" —
/// this is what silently broke FeeCollectionAppServiceV2_Tests's pre-existing tests too
/// (verified: they fail the same way, independent of this change).
/// </summary>
public class LateFeeCollectionRuntime_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
	private static readonly Guid FakeAdminUserId = Guid.Parse("2e701e62-0953-4dd3-910b-dc6cc93ccb0d");

	private readonly IFeeCollectionAppService _feeCollections;
	private readonly ICollectionPolicyAppService _collectionPolicies;
	private readonly IFeeWaiverAppService _waivers;
	private readonly IRepository<AcademicYear, Guid> _academicYearRepo;
	private readonly IRepository<EducationBoard, Guid> _boardRepo;
	private readonly IRepository<EducationMedium, Guid> _mediumRepo;
	private readonly IRepository<EducationMediumStage, Guid> _stageRepo;
	private readonly IRepository<FeeCategory, Guid> _feeCategoryRepo;
	private readonly IRepository<FeeHead, Guid> _feeHeadRepo;
	private readonly IRepository<Student, Guid> _studentRepo;
	private readonly IRepository<FeeStructureMetadata, Guid> _feeStructureMetadataRepo;
	private readonly IRepository<FeeStructure, Guid> _feeStructureRepo;
	private readonly IRepository<StudentFeeStructure, Guid> _studentFeeStructureRepo;
	private readonly IDueFeeRepository _dueFeeRepo;
	private readonly IRepository<IdentityUser, Guid> _identityUserRepo;
	private readonly IRepository<IdentityRole, Guid> _identityRoleRepo;
	private readonly ICurrentTenant _currentTenant;

	public LateFeeCollectionRuntime_Tests()
	{
		_feeCollections = GetRequiredService<IFeeCollectionAppService>();
		_collectionPolicies = GetRequiredService<ICollectionPolicyAppService>();
		_waivers = GetRequiredService<IFeeWaiverAppService>();
		_academicYearRepo = GetRequiredService<IRepository<AcademicYear, Guid>>();
		_boardRepo = GetRequiredService<IRepository<EducationBoard, Guid>>();
		_mediumRepo = GetRequiredService<IRepository<EducationMedium, Guid>>();
		_stageRepo = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		_feeCategoryRepo = GetRequiredService<IRepository<FeeCategory, Guid>>();
		_feeHeadRepo = GetRequiredService<IRepository<FeeHead, Guid>>();
		_studentRepo = GetRequiredService<IRepository<Student, Guid>>();
		_feeStructureMetadataRepo = GetRequiredService<IRepository<FeeStructureMetadata, Guid>>();
		_feeStructureRepo = GetRequiredService<IRepository<FeeStructure, Guid>>();
		_studentFeeStructureRepo = GetRequiredService<IRepository<StudentFeeStructure, Guid>>();
		_dueFeeRepo = GetRequiredService<IDueFeeRepository>();
		_identityUserRepo = GetRequiredService<IRepository<IdentityUser, Guid>>();
		_identityRoleRepo = GetRequiredService<IRepository<IdentityRole, Guid>>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	/// <summary>
	/// Idempotent — the fake principal's user/role must exist as real DB rows for
	/// FinanceScopeAccessService's admin check to resolve true, and this constructor runs once
	/// per test method against a DB shared across the whole test class.
	/// </summary>
	private async Task EnsureAdminUserExistsAsync()
	{
		if (await _identityUserRepo.FindAsync(FakeAdminUserId) is not null)
		{
			return;
		}

		var adminRole = (await _identityRoleRepo.GetListAsync(r => r.Name == "admin")).FirstOrDefault();
		if (adminRole is null)
		{
			adminRole = new IdentityRole(Guid.NewGuid(), "admin", TestTenantId);
			await _identityRoleRepo.InsertAsync(adminRole, autoSave: true);
		}

		var user = new IdentityUser(FakeAdminUserId, "admin", "admin@test.local", TestTenantId);
		user.AddRole(adminRole.Id);
		await _identityUserRepo.InsertAsync(user, autoSave: true);
	}

	private async Task<(Guid AcademicYearId, Guid StudentId, Guid FeeHeadId, Guid DueFeeId, Guid FeeStructureId)>
		SeedStudentWithOverdueDueFeeAsync(int daysOverdue, bool attachPolicy, double? maximumLateFeeAmount = null,
			bool enableLateFee = true, bool futurePaymentTerm = false)
	{
		await EnsureAdminUserExistsAsync();

		var yearId = Guid.NewGuid();
		var boardId = Guid.NewGuid();
		var mediumId = Guid.NewGuid();
		var stageId = Guid.NewGuid();
		var catId = Guid.NewGuid();
		var feeHeadId = Guid.NewGuid();
		var studentId = Guid.NewGuid();

		var year = new AcademicYear(yearId, TestTenantId, "2025-26", new DateTime(2025, 4, 1), new DateTime(2026, 3, 31));
		await _academicYearRepo.InsertAsync(year, autoSave: true);

		var board = new EducationBoard(boardId, TestTenantId, EducationBoardType.CBSE, yearId);
		await _boardRepo.InsertAsync(board, autoSave: true);

		var medium = new EducationMedium(mediumId, TestTenantId, "English", boardId);
		await _mediumRepo.InsertAsync(medium, autoSave: true);

		var stage = new EducationMediumStage(stageId, TestTenantId, "Primary", bannerPicture: null, mediumId);
		await _stageRepo.InsertAsync(stage, autoSave: true);

		var cat = new FeeCategory(catId, TestTenantId, yearId, "Tuition", "Tuition fees");
		await _feeCategoryRepo.InsertAsync(cat, autoSave: true);
		var incomeCategoryId = await FeeHeadAccountingTestData.CreateIncomeCategoryAsync(
			GetRequiredService<IRepository<AccountHead, Guid>>(),
			GetRequiredService<IRepository<TransactionCategory, Guid>>(),
			yearId, TestTenantId, "Runtime Tuition");

		var feeHead = new FeeHead(feeHeadId, TestTenantId, yearId, catId, "Monthly Fee", FeeHeadModule.Finance, false, "Monthly tuition", incomeCategoryId);
		await _feeHeadRepo.InsertAsync(feeHead, autoSave: true);

		var student = new Student(
			studentId, TestTenantId, "Test", null, "Student",
			yearId, boardId, mediumId, stageId,
			standardId: null, divisionId: null, grNo: 1, rollNo: 1,
			status: StudentLifeCycleStatusEnum.Enrolled);
		await _studentRepo.InsertAsync(student, autoSave: true);

		var metadata = new FeeStructureMetadata(Guid.NewGuid(), TestTenantId, yearId, boardId, mediumId, null, "Standard Metadata");
		await _feeStructureMetadataRepo.InsertAsync(metadata, autoSave: true);

		var feeStructure = new FeeStructure(Guid.NewGuid(), TestTenantId, metadata.Id, Guid.NewGuid(), "Standard I Fee Structure");
		await _feeStructureRepo.InsertAsync(feeStructure, autoSave: true);

		Guid? collectionPolicyId = null;
		Guid? collectionPolicyVersionId = null;

		if (attachPolicy)
		{
			var policy = await _collectionPolicies.CreateAsync(new CreateUpdateCollectionPolicyDto
			{
				AcademicYearId = yearId,
				PolicyName = "Overdue Test Policy",
				PolicyCode = $"CP-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
				Status = "Active",
				EnableLateFee = enableLateFee,
				GracePeriodDays = 0,
				LateFeeApplyOnFeeHeads = new() { feeHeadId },
				LateFeeType = "PerDay",
				AmountPerDay = 20,
				PercentageBase = "OutstandingAmount",
				CompoundFrequency = "Daily",
				RoundingMethod = "None",
				MaximumLateFeeAmount = maximumLateFeeAmount.HasValue ? (decimal)maximumLateFeeAmount.Value : null,
				AllowOnlineCollection = true,
			});
			policy = await _collectionPolicies.GetAsync(policy.Id);
			policy.CurrentVersionId.ShouldNotBeNull();
			collectionPolicyId = policy.Id;
			collectionPolicyVersionId = policy.CurrentVersionId;

			using (_currentTenant.Change(TestTenantId))
			{
				var reloadedStructure = await _feeStructureRepo.GetAsync(feeStructure.Id);
				reloadedStructure.AttachCollectionPolicy(policy.Id);
				await _feeStructureRepo.UpdateAsync(reloadedStructure, autoSave: true);
			}
		}

		// Existing collections must use the assignment's pinned policy, never current mutable settings.
		var assignment = new StudentFeeStructure(Guid.NewGuid(), TestTenantId, studentId, feeStructure.Id, yearId,
			null, null, ConcessionFeeHeadScope.EntireFeeStructure, [], collectionPolicyId, collectionPolicyVersionId);
		await _studentFeeStructureRepo.InsertAsync(assignment, autoSave: true);

		var demandDate = futurePaymentTerm ? DateTime.UtcNow.Date.AddMonths(1) : (DateTime?)null;
		var dueDate = demandDate?.AddDays(10) ?? DateTime.UtcNow.Date.AddDays(-daysOverdue);
		var dueFee = new DueFee(
			id: Guid.NewGuid(), tenantId: TestTenantId, studentId: studentId, feeHeadId: feeHeadId,
			month: (MonthEnum)(dueDate.Month - 1), year: dueDate.Year, isOneTime: false, amount: 5000m, dueDate: dueDate,
			installmentSequence: futurePaymentTerm ? 1 : null,
			installmentName: futurePaymentTerm ? "Future installment" : null,
			demandDate: demandDate, scheduleDueDate: futurePaymentTerm ? dueDate : null,
			scheduleSource: futurePaymentTerm ? ScheduleSource.PaymentTerm : ScheduleSource.Legacy);
		dueFee.BindAssignment(assignment.Id);
		await _dueFeeRepo.InsertAsync(dueFee, autoSave: true);

		return (yearId, studentId, feeHeadId, dueFee.Id, feeStructure.Id);
	}

	[Fact]
	public async Task Collect_Form_Rejects_An_Assignment_Without_A_Pinned_Collection_Policy()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (academicYearId, studentId, _, _, _) = await SeedStudentWithOverdueDueFeeAsync(daysOverdue: 30, attachPolicy: false);
			var error = await Should.ThrowAsync<BusinessException>(() => _feeCollections.GetCollectFormAsync(studentId, academicYearId));
			error.Code.ShouldBe(CollectionPolicyErrorCodes.PolicyVersionNotFound);
		}
	}

	[Fact]
	public async Task Collect_Form_Shows_Zero_Late_Fee_When_The_Pinned_Policy_Disables_It()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (academicYearId, studentId, _, dueFeeId, _) = await SeedStudentWithOverdueDueFeeAsync(daysOverdue: 30, attachPolicy: true, enableLateFee: false);
			var form = await _feeCollections.GetCollectFormAsync(studentId, academicYearId);

			var line = form.Ledger.Should_ContainLine(dueFeeId);
			line.LateFeeAmount.ShouldBe(0);
			form.AllowOnlineCollection.ShouldBeTrue();
		}
	}

	[Fact]
	public async Task Collect_Form_Includes_Future_Payment_Term_Installments_For_Advance_Collection()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (academicYearId, studentId, _, futureDueFeeId, _) = await SeedStudentWithOverdueDueFeeAsync(
				daysOverdue: 0, attachPolicy: true, enableLateFee: false, futurePaymentTerm: true);

			var form = await _feeCollections.GetCollectFormAsync(studentId, academicYearId);

			var line = form.Ledger.Should_ContainLine(futureDueFeeId);
			line.Due.ShouldBe(5_000d);
			line.LateFeeAmount.ShouldBe(0d);
			form.Summary.PendingFee.ShouldBe(5_000d);

			var output = await _feeCollections.CollectFeeAsync(new CollectFeeInput
			{
				IdempotencyKey = Guid.NewGuid().ToString("N"),
				StudentId = studentId,
				AcademicYearId = academicYearId,
				DueFeePayments = [new() { DueFeeId = futureDueFeeId, FeeHeadId = line.FeeHeadId, Amount = 5_000d }],
				Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 5_000d }],
			});
			output.PaidDueFees.ShouldContain(x => x.DueFeeId == futureDueFeeId && x.AmountPaid == 5_000d);
		}
	}

	[Fact]
	public async Task Collect_Form_Computes_Late_Fee_Using_The_Same_Policy_As_Simulate()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (academicYearId, studentId, _, dueFeeId, _) = await SeedStudentWithOverdueDueFeeAsync(daysOverdue: 15, attachPolicy: true);

			var form = await _feeCollections.GetCollectFormAsync(studentId, academicYearId);

			var line = form.Ledger.Should_ContainLine(dueFeeId);
			line.DueFeeId.ShouldBe(dueFeeId);
			line.LateFeeAmount.ShouldBe(0); // late fees are projected as system charges, not ledger columns
			line.CanWaive.ShouldBeFalse();
			var lateFeeCharge = form.Charges.Single(x => x.SourceType == "LateFee" && x.SourceId == dueFeeId);
			lateFeeCharge.PendingAmount.ShouldBe(300); // 15 days * ₹20/day, 0-day grace
			lateFeeCharge.IsSystemGenerated.ShouldBeTrue();
			lateFeeCharge.CanWaive.ShouldBeTrue();
			lateFeeCharge.Explanation.ShouldNotBeNullOrWhiteSpace();
		}
	}

	[Fact]
	public async Task Collect_Form_Respects_Maximum_Late_Fee_Cap()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (academicYearId, studentId, _, dueFeeId, _) = await SeedStudentWithOverdueDueFeeAsync(
				daysOverdue: 100, attachPolicy: true, maximumLateFeeAmount: 500);

			var form = await _feeCollections.GetCollectFormAsync(studentId, academicYearId);

			var lateFeeCharge = form.Charges.Single(x => x.SourceType == "LateFee" && x.SourceId == dueFeeId);
			lateFeeCharge.PendingAmount.ShouldBe(500); // raw would be 2000, capped
		}
	}

	[Fact]
	public async Task Applying_A_Waiver_Reduces_The_Late_Fee_Shown_On_The_Collect_Form()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (academicYearId, studentId, _, dueFeeId, _) = await SeedStudentWithOverdueDueFeeAsync(daysOverdue: 10, attachPolicy: true);

			await _waivers.ApplyAsync(new ApplyFeeWaiverInput
			{
				SourceType = "LateFee",
				SourceId = dueFeeId,
				Reason = "Family financial hardship — documented.",
				IdempotencyKey = Guid.NewGuid().ToString("N"),
			});

			var form = await _feeCollections.GetCollectFormAsync(studentId, academicYearId);
			var lateFeeCharge = form.Charges.Single(x => x.SourceType == "LateFee" && x.SourceId == dueFeeId);

			lateFeeCharge.PendingAmount.ShouldBe(0); // full remaining balance waived
			lateFeeCharge.WaivedAmount.ShouldBe(200);
			lateFeeCharge.CanWaive.ShouldBeFalse();
			lateFeeCharge.Status.ShouldBe(FeeCollectionStatus.Paid);
		}
	}

	[Fact]
	public async Task Waiver_Without_A_Reason_Is_Rejected()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (_, _, _, dueFeeId, _) = await SeedStudentWithOverdueDueFeeAsync(daysOverdue: 10, attachPolicy: true);

			var ex = await Should.ThrowAsync<BusinessException>(() => _waivers.ApplyAsync(new ApplyFeeWaiverInput
			{
				SourceType = "LateFee",
				SourceId = dueFeeId,
				Reason = "",
				IdempotencyKey = Guid.NewGuid().ToString("N"),
			}));

			ex.Code.ShouldBe(FeeWaiverErrorCodes.ReasonRequired);
		}
	}

	[Fact]
	public async Task Waiver_Is_Limited_Only_By_The_Remaining_Late_Fee()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (_, _, _, dueFeeId, _) = await SeedStudentWithOverdueDueFeeAsync(daysOverdue: 15, attachPolicy: true);

			var result = await _waivers.ApplyAsync(new ApplyFeeWaiverInput
			{
				SourceType = "LateFee",
				SourceId = dueFeeId,
				Reason = "Requesting full waiver.",
				IdempotencyKey = Guid.NewGuid().ToString("N"),
			});

			result.WaivedAmount.ShouldBe(300);
			result.RemainingAmount.ShouldBe(0);
		}
	}

	[Fact]
	public async Task Collecting_A_Late_Fee_Payment_Within_The_Computed_Ceiling_Succeeds()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (academicYearId, studentId, feeHeadId, dueFeeId, _) = await SeedStudentWithOverdueDueFeeAsync(daysOverdue: 10, attachPolicy: true);

			var output = await _feeCollections.CollectFeeAsync(new CollectFeeInput
			{
				IdempotencyKey = Guid.NewGuid().ToString("N"),
				StudentId = studentId,
				AcademicYearId = academicYearId,
				DueFeePayments = [new() { DueFeeId = dueFeeId, FeeHeadId = feeHeadId, Amount = 1000d }],
				LateFeePayments = [new() { DueFeeId = dueFeeId, Amount = 200d }], // exactly the computed ceiling
				Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 1200d }],
			});

			output.PaidLateFees.ShouldHaveSingleItem();
			output.PaidLateFees[0].AmountPaid.ShouldBe(200d);
			output.ReceivedAmount.ShouldBe(1200d);
		}
	}

	[Fact]
	public async Task Collecting_A_Late_Fee_Payment_Above_The_Computed_Ceiling_Is_Rejected()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (academicYearId, studentId, feeHeadId, dueFeeId, _) = await SeedStudentWithOverdueDueFeeAsync(daysOverdue: 10, attachPolicy: true);

			var ex = await Should.ThrowAsync<BusinessException>(() => _feeCollections.CollectFeeAsync(new CollectFeeInput
			{
				IdempotencyKey = Guid.NewGuid().ToString("N"),
				StudentId = studentId,
				AcademicYearId = academicYearId,
				DueFeePayments = [new() { DueFeeId = dueFeeId, FeeHeadId = feeHeadId, Amount = 1000d }],
				LateFeePayments = [new() { DueFeeId = dueFeeId, Amount = 5000d }], // computed ceiling is only 200
				Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 6000d }],
			}));

			ex.Code.ShouldBe(FeeCollectionErrorCodes.LateFeeAmountExceedsComputed);
		}
	}
}

internal static class LateFeeCollectionRuntimeTestExtensions
{
	public static FeeLedgerLineDto Should_ContainLine(this List<FeeLedgerMonthDto> ledger, Guid dueFeeId)
	{
		foreach (var month in ledger)
		{
			var match = month.Items.Find(i => i.DueFeeId == dueFeeId);
			if (match is not null)
			{
				return match;
			}
		}

		throw new InvalidOperationException($"No ledger line found for DueFeeId {dueFeeId}");
	}
}
