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
public partial class LateFeeCollectionRuntime_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
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
	private readonly IRepository<ProrationPolicy, Guid> _prorationPolicyRepo;
	private readonly IRepository<FinancePolicyVersion, Guid> _policyVersionRepo;
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
		_prorationPolicyRepo = GetRequiredService<IRepository<ProrationPolicy, Guid>>();
		_policyVersionRepo = GetRequiredService<IRepository<FinancePolicyVersion, Guid>>();
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
			bool enableLateFee = true, bool futurePaymentTerm = false, bool attachProrationPolicy = false,
		bool percentageLateFee = false, decimal principalAmount = 5000m, string? allocationBasis = null, string? academicYearName = null)
	{
		await EnsureAdminUserExistsAsync();

		var yearId = Guid.NewGuid();
		var boardId = Guid.NewGuid();
		var mediumId = Guid.NewGuid();
		var stageId = Guid.NewGuid();
		var catId = Guid.NewGuid();
		var feeHeadId = Guid.NewGuid();
		var studentId = Guid.NewGuid();

		var year = new AcademicYear(yearId, TestTenantId, academicYearName ?? "2025-26", new DateTime(2025, 4, 1), new DateTime(2026, 3, 31));
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
				LateFeeIncomeCategoryId = incomeCategoryId,
				GracePeriodDays = 0,
				LateFeeApplyOnFeeHeads = new() { feeHeadId },
				LateFeeType = percentageLateFee ? "Percentage" : "PerDay",
				Percentage = percentageLateFee ? 10m : null,
				AmountPerDay = 20,
				PercentageBase = "OutstandingAmount",
				CompoundFrequency = "Daily",
				RoundingMethod = "None",
				MaximumLateFeeAmount = maximumLateFeeAmount.HasValue ? (decimal)maximumLateFeeAmount.Value : null,
				AllowOnlineCollection = true,
				AllocationBasis = allocationBasis,
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
		Guid? prorationPolicyId = null;
		Guid? prorationVersionId = null;
		if (attachProrationPolicy)
		{
			prorationPolicyId = Guid.NewGuid();
			var policy = new ProrationPolicy(prorationPolicyId.Value, TestTenantId, yearId,
				new ProrationPolicyUpdateModel("Current policy name", "PROR-TEST", null,
					ProrationPolicyStatus.Active, false,
					[new ProrationPolicyRuleModel(Guid.NewGuid(), feeHeadId,
						ProrationRuleType.FullCharge, ProrationRoundingMode.Currency, null, null)]));
			await _prorationPolicyRepo.InsertAsync(policy, autoSave: true);
			prorationVersionId = Guid.NewGuid();
			await _policyVersionRepo.InsertAsync(new FinancePolicyVersion(prorationVersionId.Value,
				TestTenantId, "proration", prorationPolicyId.Value, yearId, 2,
				"Original policy name", "Active", "{}", "changed"), autoSave: true);
		}
		var dueFee = new DueFee(
			id: Guid.NewGuid(), tenantId: TestTenantId, studentId: studentId, feeHeadId: feeHeadId,
			month: (MonthEnum)(dueDate.Month - 1), year: dueDate.Year, isOneTime: false, amount: principalAmount, dueDate: dueDate,
			installmentSequence: futurePaymentTerm ? 1 : null,
			installmentName: futurePaymentTerm ? "Future installment" : null,
			demandDate: demandDate, scheduleDueDate: futurePaymentTerm ? dueDate : null,
			scheduleSource: futurePaymentTerm ? ScheduleSource.PaymentTerm : ScheduleSource.Legacy,
			prorationPolicyId: prorationPolicyId, prorationPolicyVersionId: prorationVersionId,
			prorationExplanation: attachProrationPolicy ? "Full scheduled charge applies." : null);
		dueFee.BindAssignment(assignment.Id);
		await _dueFeeRepo.InsertAsync(dueFee, autoSave: true);

		return (yearId, studentId, feeHeadId, dueFee.Id, feeStructure.Id);
	}

	private async Task<FeeCollectionSummaryItemDto> GetSummaryRowAsync(Guid yearId, Guid studentId)
	{
		var student = await _studentRepo.GetAsync(studentId);
		return (await _feeCollections.GetSummaryAsync(new GetFeeCollectionSummaryInput
		{
			AcademicYearId = yearId, BoardId = student.AcademicBoardId!.Value,
			EducationMediumId = student.AcademicMediumId, MaxResultCount = 100,
		})).Items.Single(x => x.StudentId == studentId);
	}

	[Theory]
	[InlineData("Installment")]
	[InlineData("FeeHead")]
	public async Task Allocation_Basis_Survives_Save_Update_And_Clone(string basis)
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, student, _, _, _) = await SeedStudentWithOverdueDueFeeAsync(10, true,
				enableLateFee: false, allocationBasis: basis);
			var initialForm = await _feeCollections.GetCollectFormAsync(student, year);
			var saved = await _collectionPolicies.GetAsync(initialForm.CollectionPolicyId!.Value);
			saved.AllocationBasis.ShouldBe(basis);
			var update = new CreateUpdateCollectionPolicyDto
			{
				AcademicYearId = year, PolicyName = saved.PolicyName, PolicyCode = saved.PolicyCode,
				Status = "Active", IsDefault = saved.IsDefault, EnableLateFee = false, AllocationBasis = null
			};
			(await _collectionPolicies.UpdateAsync(saved.Id, update)).AllocationBasis.ShouldBe(basis);
			(await _collectionPolicies.CloneAsync(saved.Id, new CollectionPolicyCloneRequestDto())).AllocationBasis.ShouldBe(basis);
			// The original assignment still resolves the version captured at assignment time.
			(await _feeCollections.GetCollectFormAsync(student, year)).AllocationBasis.ShouldBe(basis);
		}
	}

	[Theory]
	[InlineData("Installment")]
	[InlineData("FeeHead")]
	public async Task Automatic_Allocation_Rejects_Tampering_And_Stale_Review_And_Replays_Safely(string basis)
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, student, head, firstId, _) = await SeedStudentWithOverdueDueFeeAsync(10, true,
				enableLateFee: false, principalAmount: 100m, allocationBasis: basis);
			var first = await _dueFeeRepo.GetAsync(firstId);
			var date = first.DueDate.AddMonths(1);
			var second = new DueFee(Guid.NewGuid(), TestTenantId, student, head,
				(MonthEnum)(date.Month - 1), date.Year, false, 100m, date);
			second.BindAssignment(first.StudentFeeStructureId!.Value);
			await _dueFeeRepo.InsertAsync(second, autoSave: true);
			var form = await _feeCollections.GetCollectFormAsync(student, year);
			form.AllocationBasis.ShouldBe(basis);
			form.AllocationDueFeeOrder.ShouldBe(new[] { firstId, second.Id });
			var command = new CollectFeeInput
			{
				StudentId = student, AcademicYearId = year, IdempotencyKey = Guid.NewGuid().ToString("N"),
				AutomaticAllocation = new() { Revision = form.AllocationRevision!, DueFeeIds = [firstId, second.Id] },
				DueFeePayments = [new() { DueFeeId = firstId, FeeHeadId = head, Amount = 50 }, new() { DueFeeId = second.Id, FeeHeadId = head, Amount = 75 }],
				Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 125 }]
			};
			(await Should.ThrowAsync<BusinessException>(() => _feeCollections.CollectFeeAsync(command))).Code.ShouldBe(FeeCollectionErrorCodes.AllocationChanged);
			(await _dueFeeRepo.GetAsync(firstId)).PaidAmount.ShouldBe(0m);
			command.DueFeePayments[0].Amount = 100; command.DueFeePayments[1].Amount = 25;
			command.AutomaticAllocation.Revision = "stale";
			(await Should.ThrowAsync<BusinessException>(() => _feeCollections.CollectFeeAsync(command))).Code.ShouldBe(FeeCollectionErrorCodes.AllocationChanged);
			command.AutomaticAllocation.Revision = form.AllocationRevision!;
			var result = await _feeCollections.CollectFeeAsync(command);
			result.ReceivedAmount.ShouldBe(125);
			(await _feeCollections.CollectFeeAsync(command)).Id.ShouldBe(result.Id);
			(await _dueFeeRepo.GetAsync(firstId)).OutstandingAmount.ShouldBe(0m);
			(await _dueFeeRepo.GetAsync(second.Id)).OutstandingAmount.ShouldBe(75m);
		}
	}

	[Theory]
	[InlineData(1, FeeCollectionStatus.Overdue, 5000d)]
	[InlineData(0, FeeCollectionStatus.Pending, 0d)]
	[InlineData(-1, FeeCollectionStatus.Pending, 0d)]
	public async Task Grid_Details_And_Ledger_Use_The_Same_Actual_Due_Date(int daysOverdue, FeeCollectionStatus expected, double overdueAmount)
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (yearId, studentId, _, dueId, _) = await SeedStudentWithOverdueDueFeeAsync(daysOverdue, true, enableLateFee: false);
			var due = await _dueFeeRepo.GetAsync(dueId);
			var form = await _feeCollections.GetCollectFormAsync(studentId, yearId);
			var line = form.Ledger.SelectMany(x => x.Items).Single();
			line.Status.ShouldBe(expected); line.DueDate.ShouldBe(due.DueDate);
			(await GetSummaryRowAsync(yearId, studentId)).OverdueFees.ShouldBe(overdueAmount);
			var details = await _feeCollections.GetStudentFeeDetailsAsync(studentId, yearId);
			(details.FeeOverview.OverdueFee ?? 0d).ShouldBe(overdueAmount);
		}
	}

	[Fact]
	public async Task Partially_Paid_Overdue_Principal_Remains_Visible_As_Overdue()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (yearId, studentId, _, dueId, _) = await SeedStudentWithOverdueDueFeeAsync(1, true, enableLateFee: false);
			var due = await _dueFeeRepo.GetAsync(dueId); due.ApplyPayment(1000m);
			await _dueFeeRepo.UpdateAsync(due, autoSave: true);
			var form = await _feeCollections.GetCollectFormAsync(studentId, yearId);
			form.Ledger.SelectMany(x => x.Items).Single().Status.ShouldBe(FeeCollectionStatus.Overdue);
			(await GetSummaryRowAsync(yearId, studentId)).OverdueFees.ShouldBe(4000d);
		}
	}

	[Fact]
	public async Task Uncleared_Late_Fee_Allocation_Uses_Reconciliation_Status_Without_Changing_Pending_Principal()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (yearId, studentId, _, dueId, _) = await SeedStudentWithOverdueDueFeeAsync(10, true);
			var metadata = new FeeRecordMetadata(Guid.NewGuid(), TestTenantId, studentId, yearId,
				200d, $"RECON-{Guid.NewGuid():N}", FeeCollectionStatus.InReconciliation);
			await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(metadata, autoSave: true);
			await GetRequiredService<IRepository<LateFeeCollectionAllocation, Guid>>().InsertAsync(
				new LateFeeCollectionAllocation(Guid.NewGuid(), TestTenantId, metadata.Id, dueId, 200d), autoSave: true);
			var form = await _feeCollections.GetCollectFormAsync(studentId, yearId);
			var charge = form.Charges.Single();
			charge.Status.ShouldBe(FeeCollectionStatus.InReconciliation);
			charge.ReconciliationAmount.ShouldBe(200m); charge.PendingAmount.ShouldBe(0d);
			form.Ledger.SelectMany(x => x.Items).Single().Status.ShouldBe(FeeCollectionStatus.Overdue);
		}
	}

	[Fact]
	public async Task Six_Fee_Items_Of_12396_Leave_Four_Unselected_100_Rupee_Charges_Pending()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (yearId, studentId, headId, dueId, _) = await SeedStudentWithOverdueDueFeeAsync(
				10, true, maximumLateFeeAmount: 100, principalAmount: 1999.2m);
			var first = await _dueFeeRepo.GetAsync(dueId);
			var dues = new List<DueFee> { first };
			foreach (var (offset, amount) in new[] { (-40, 1999.2m), (-70, 1999.2m), (-100, 2400m), (30, 1999.2m), (60, 1999.2m) })
			{
				var date = DateTime.UtcNow.Date.AddDays(offset);
				var due = new DueFee(Guid.NewGuid(), TestTenantId, studentId, headId,
					(MonthEnum)(date.Month - 1), date.Year, false, amount, date);
				due.BindAssignment(first.StudentFeeStructureId!.Value);
				await _dueFeeRepo.InsertAsync(due, autoSave: true); dues.Add(due);
			}
			var initial = await GetSummaryRowAsync(yearId, studentId);
			initial.TotalCharges.ShouldBe(400d); initial.PendingCharges.ShouldBe(400d);
			var fees = await _feeCollections.CollectFeeAsync(new CollectFeeInput
			{
				StudentId = studentId, AcademicYearId = yearId, IdempotencyKey = Guid.NewGuid().ToString("N"),
				DueFeePayments = dues.Select(d => new DueFeePaymentDto
				{ DueFeeId = d.Id, FeeHeadId = headId, Amount = (double)d.OutstandingAmount }).ToList(),
				Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 12396d }],
			});
			fees.ReceivedAmount.ShouldBe(12396d, 0.000001d); fees.PaidDueFees.Count.ShouldBe(6); fees.PaidLateFees.ShouldBeEmpty();
			var remaining = (await _feeCollections.GetCollectFormAsync(studentId, yearId)).Charges;
			remaining.Count.ShouldBe(4); remaining.ShouldAllBe(c => c.PendingAmount == 100d && c.Status == FeeCollectionStatus.Pending);
			(await GetSummaryRowAsync(yearId, studentId)).PendingCharges.ShouldBe(400d);
		}
	}

	[Fact]
	public async Task Grid_Includes_System_Charges_Before_Opening_The_Form_And_Exports_The_Same_Totals()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (yearId, studentId, _, dueId, _) = await SeedStudentWithOverdueDueFeeAsync(10, true);
			var row = await GetSummaryRowAsync(yearId, studentId);
			row.TotalCharges.ShouldBe(200d); row.PendingCharges.ShouldBe(200d);
			(await _dueFeeRepo.GetAsync(dueId)).GetAssessedLateFeeAmount().ShouldBe(0m); // read is side-effect free
			var form = await _feeCollections.GetCollectFormAsync(studentId, yearId);
			row.TotalCharges.ShouldBe(form.Charges.Sum(x => x.Amount));
			row.PendingCharges.ShouldBe(form.Charges.Sum(x => x.PendingAmount));
			var details = await _feeCollections.GetStudentFeeDetailsAsync(studentId, yearId);
			details.Charges!.Sum(x => x.PendingAmount).ShouldBe(200d);
			var student = await _studentRepo.GetAsync(studentId);
			var csv = System.Text.Encoding.UTF8.GetString(await _feeCollections.ExportAsync(new FeeCollectionExportRequestDto
			{
				AcademicYearId = yearId, BoardId = student.AcademicBoardId!.Value, EducationMediumId = student.AcademicMediumId,
				Format = FeeCollectionExportFormat.Csv,
			}));
			csv.ShouldContain(",200,200,");
		}
	}

	[Fact]
	public async Task Principal_Only_Payment_Preserves_Charges_For_Later_Collection_And_Reversal()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (yearId, studentId, headId, dueId, _) = await SeedStudentWithOverdueDueFeeAsync(10, true);
			var principal = await _feeCollections.CollectFeeAsync(new CollectFeeInput
			{
				IdempotencyKey = Guid.NewGuid().ToString("N"), StudentId = studentId, AcademicYearId = yearId,
				DueFeePayments = [new() { DueFeeId = dueId, FeeHeadId = headId, Amount = 5000d }],
				Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 5000d }],
			});
			principal.PaidLateFees.ShouldBeEmpty();
			using (var reload = GetRequiredService<Volo.Abp.Uow.IUnitOfWorkManager>().Begin(new Volo.Abp.Uow.AbpUnitOfWorkOptions(), requiresNew: true))
			{
				var storedDue = await _dueFeeRepo.GetAsync(dueId);
				storedDue.OutstandingAmount.ShouldBe(0m);
				storedDue.GetAssessedLateFeeAmount().ShouldBe(200m);
				storedDue.GetLateFeeAssessmentExplanation().ShouldNotBeNullOrWhiteSpace();
				await reload.CompleteAsync();
			}
			var form = await _feeCollections.GetCollectFormAsync(studentId, yearId);
			var charge = form.Charges.Single(x => x.SourceId == dueId);
			charge.PendingAmount.ShouldBe(200d); charge.Status.ShouldBe(FeeCollectionStatus.Pending);
			form.Summary.PendingFee.ShouldBe(0d);
			(await GetSummaryRowAsync(yearId, studentId)).PendingCharges.ShouldBe(200d);
			(await _feeCollections.GetCollectFormAsync(studentId, yearId)).Charges.Single().PendingAmount.ShouldBe(200d);
			(await GetSummaryRowAsync(yearId, studentId)).TotalCharges.ShouldBe(200d);
			var late = await _feeCollections.CollectFeeAsync(new CollectFeeInput
			{
				IdempotencyKey = Guid.NewGuid().ToString("N"), StudentId = studentId, AcademicYearId = yearId,
				LateFeePayments = [new() { DueFeeId = dueId, Amount = 200d }],
				Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 200d }],
			});
			late.PaidDueFees.ShouldBeEmpty(); late.PaidLateFees.Single().AmountPaid.ShouldBe(200d);
			var settled = await GetSummaryRowAsync(yearId, studentId);
			settled.TotalCharges.ShouldBe(200d); settled.PendingCharges.ShouldBe(0d);
			var metadataRepo = GetRequiredService<IRepository<FeeRecordMetadata, Guid>>();
			var tx = await metadataRepo.GetAsync(late.Id); tx.MarkReversed(); await metadataRepo.UpdateAsync(tx, autoSave: true);
			(await GetSummaryRowAsync(yearId, studentId)).PendingCharges.ShouldBe(200d);
		}
	}

	[Fact]
	public async Task Partial_Principal_Payment_Cannot_Reduce_An_Accrued_Percentage_Charge_And_Waiver_Works_After_Settlement()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (yearId, studentId, headId, dueId, _) = await SeedStudentWithOverdueDueFeeAsync(10, true, percentageLateFee: true);
			foreach (var amount in new[] { 4000d, 1000d })
			{
				await _feeCollections.CollectFeeAsync(new CollectFeeInput
				{
					IdempotencyKey = Guid.NewGuid().ToString("N"), StudentId = studentId, AcademicYearId = yearId,
					DueFeePayments = [new() { DueFeeId = dueId, FeeHeadId = headId, Amount = amount }],
					Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = amount }],
				});
				(await GetSummaryRowAsync(yearId, studentId)).PendingCharges.ShouldBe(500d);
			}
			await _waivers.ApplyAsync(new ApplyFeeWaiverInput
			{ SourceType = "LateFee", SourceId = dueId, Reason = "Approved hardship", IdempotencyKey = Guid.NewGuid().ToString("N") });
			var row = await GetSummaryRowAsync(yearId, studentId);
			row.TotalCharges.ShouldBe(500d); row.PendingCharges.ShouldBe(0d);
			(await _feeCollections.GetCollectFormAsync(studentId, yearId)).Charges.Single().Status.ShouldBe(FeeCollectionStatus.Waived);
		}
	}

	[Theory]
	[InlineData(false, false, false)]
	[InlineData(true, false, false)]
	[InlineData(false, true, false)]
	[InlineData(false, false, true)]
	public async Task Pre_Fix_Principal_Payment_Recovers_Charges_From_Its_Original_Date_And_Pending_Before_Evidence(
		bool policyChangedAfterPayment, bool overrideChangedAfterPayment, bool windowCreatedAfterPayment)
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (yearId, studentId, _, dueId, structureId) = await SeedStudentWithOverdueDueFeeAsync(30, true);
			var due = await _dueFeeRepo.GetAsync(dueId);
			var paidOn = due.DueDate.AddDays(10);
			due.ApplyPayment(5000m); await _dueFeeRepo.UpdateAsync(due, autoSave: true);
			var tx = new FeeRecordMetadata(Guid.NewGuid(), TestTenantId, studentId, yearId, 5000d, "LEGACY-REC", FeeCollectionStatus.Paid);
			tx.SetCommandResult(System.Text.Json.JsonSerializer.Serialize(new
			{ CollectedAt = paidOn, AuditSummary = new { AllocationLines = new[] { new { DueFeeId = dueId, PendingBefore = 5000m } } } }));
			await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(tx, autoSave: true);
			await GetRequiredService<IRepository<FeeCollectionDueFeeAllocation, Guid>>().InsertAsync(
				new FeeCollectionDueFeeAllocation(Guid.NewGuid(), TestTenantId, tx.Id, dueId, 5000d), autoSave: true);
			if (overrideChangedAfterPayment)
			{
				var overrideRepo = GetRequiredService<IRepository<StudentFeePolicyOverride, Guid>>();
				var studentOverride = new StudentFeePolicyOverride(Guid.NewGuid(), TestTenantId, studentId, structureId)
				{ CreationTime = paidOn.AddDays(-1) };
				studentOverride.Update(true, null, false, null); // exemption at the historical payment
				await overrideRepo.InsertAsync(studentOverride, autoSave: true);
				studentOverride.Update(false, 0, false, null); // later removal must not create historical debt
				await overrideRepo.UpdateAsync(studentOverride, autoSave: true);
			}
			if (windowCreatedAfterPayment)
				await GetRequiredService<IRepository<CollectionPolicyExceptionWindow, Guid>>().InsertAsync(
					new CollectionPolicyExceptionWindow(Guid.NewGuid(), TestTenantId, ExceptionWindowScope.AcademicYear,
						yearId, due.DueDate, GetRequiredService<Volo.Abp.Timing.IClock>().Now.AddDays(1), ExceptionWindowEffect.ExtendGracePeriod, 1,
						"BACKDATED", "Created after the historical payment", Guid.NewGuid()), autoSave: true);
			if (policyChangedAfterPayment)
			{
				var assignment = (await _studentFeeStructureRepo.GetListAsync(x => x.StudentId == studentId)).Single();
				assignment.ExtraProperties["AssignedPolicyHistory"] = System.Text.Json.JsonSerializer.Serialize(new[]
				{
					new { OccurredAt = paidOn.AddDays(1), Before = new { CollectionPolicyVersionId = Guid.NewGuid() },
						After = new { assignment.CollectionPolicyVersionId } }
				});
				await _studentFeeStructureRepo.UpdateAsync(assignment, autoSave: true);
				(await GetSummaryRowAsync(yearId, studentId)).PendingCharges.ShouldBe(0d);
				(await _feeCollections.GetCollectFormAsync(studentId, yearId)).Charges.ShouldBeEmpty();
			}
			else if (overrideChangedAfterPayment || windowCreatedAfterPayment)
			{
				(await GetSummaryRowAsync(yearId, studentId)).PendingCharges.ShouldBe(0d);
				(await _feeCollections.GetCollectFormAsync(studentId, yearId)).Charges.ShouldBeEmpty();
			}
			else
			{
				(await GetSummaryRowAsync(yearId, studentId)).PendingCharges.ShouldBe(200d); // 10 days, not today's 30 days
				(await _feeCollections.GetCollectFormAsync(studentId, yearId)).Charges.Single().PendingAmount.ShouldBe(200d);
			}
		}
	}

	[Theory]
	[InlineData("invalid-json", null)]
	[InlineData("non-array-lines", null)]
	[InlineData(null, "invalid-json")]
	[InlineData(null, "{}")]
	public async Task Malformed_Legacy_Recovery_Evidence_Does_Not_Invent_Charges_Or_Break_Collections(string? metadataShape, string? history)
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (yearId, studentId, _, dueId, _) = await SeedStudentWithOverdueDueFeeAsync(30, true);
			var due = await _dueFeeRepo.GetAsync(dueId);
			due.ApplyPayment(5000m); await _dueFeeRepo.UpdateAsync(due, autoSave: true);
			var tx = new FeeRecordMetadata(Guid.NewGuid(), TestTenantId, studentId, yearId, 5000d, "LEGACY-REC", FeeCollectionStatus.Paid);
			var json = metadataShape switch
			{
				"invalid-json" => "{broken",
				"non-array-lines" => System.Text.Json.JsonSerializer.Serialize(new
				{ CollectedAt = due.DueDate.AddDays(10), AuditSummary = new { AllocationLines = new { DueFeeId = dueId } } }),
				_ => System.Text.Json.JsonSerializer.Serialize(new
				{ CollectedAt = due.DueDate.AddDays(10), AuditSummary = new { AllocationLines = new[] { new { DueFeeId = dueId, PendingBefore = 5000m } } } }),
			};
			tx.SetCommandResult(json);
			await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(tx, autoSave: true);
			await GetRequiredService<IRepository<FeeCollectionDueFeeAllocation, Guid>>().InsertAsync(
				new FeeCollectionDueFeeAllocation(Guid.NewGuid(), TestTenantId, tx.Id, dueId, 5000d), autoSave: true);
			if (history is not null)
			{
				var assignment = (await _studentFeeStructureRepo.GetListAsync(x => x.StudentId == studentId)).Single();
				assignment.ExtraProperties["AssignedPolicyHistory"] = history;
				await _studentFeeStructureRepo.UpdateAsync(assignment, autoSave: true);
			}
			(await GetSummaryRowAsync(yearId, studentId)).PendingCharges.ShouldBe(0d);
			(await _feeCollections.GetCollectFormAsync(studentId, yearId)).Charges.ShouldBeEmpty();
		}
	}

	[Fact]
	public async Task Collect_Form_Uses_The_Pinned_Proration_Version_Label()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (academicYearId, studentId, _, dueFeeId, _) = await SeedStudentWithOverdueDueFeeAsync(
				daysOverdue: 0, attachPolicy: true, attachProrationPolicy: true);
			var line = (await _feeCollections.GetCollectFormAsync(studentId, academicYearId))
				.Ledger.Should_ContainLine(dueFeeId);

			line.ProrationPolicyName.ShouldBe("Original policy name");
			line.ProrationPolicyVersionNumber.ShouldBe(2);
		}
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
			lateFeeCharge.Status.ShouldBe(FeeCollectionStatus.Waived);
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
