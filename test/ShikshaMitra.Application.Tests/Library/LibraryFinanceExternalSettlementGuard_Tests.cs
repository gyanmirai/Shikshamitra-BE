using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Legacy_Refund_Eligibility_Should_Exclude_External_Charges_Even_For_A_Fee_Assigned_Student()
	{
		var fixture = await SeedAsync(FinanceExternalPayerKind.Student, false);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var actor = SignIn(fixture);
		var posted = await WithUnitOfWorkAsync(() => Service(fixture).PostAsync(Post(fixture)));
		var legacyChargeId = await SeedNormallyEligiblePaidStudentChargeAsync(fixture, posted);

		var eligibility = await WithUnitOfWorkAsync(() => RefundService(fixture).GetEligibilityAsync(fixture.PersonId, fixture.YearId));

		eligibility.TotalRefundable.ShouldBe(70m);
		eligibility.FeeHeads.SelectMany(head => head.Months).ShouldContain(row => row.ChargeId == legacyChargeId && row.Refundable == 70m);
		eligibility.FeeHeads.SelectMany(head => head.Months).ShouldNotContain(row => row.ChargeId == posted.FinanceChargeId);
	}

	private FeeRefundAppService RefundService(Fixture fixture)
	{
		var services = new FinanceServices(ServiceProvider, fixture);
		var service = ActivatorUtilities.CreateInstance<FeeRefundAppService>(services);
		service.LazyServiceProvider = new AbpLazyServiceProvider(services);
		return service;
	}

	private Task<Guid> SeedNormallyEligiblePaidStudentChargeAsync(Fixture fixture, FinanceExternalReceivableDto posted) =>
		WithUnitOfWorkAsync(async () =>
		{
			var students = GetRequiredService<IRepository<Student, Guid>>();
			var student = await students.GetAsync(fixture.PersonId);
			var stageId = student.AcademicMediumStageId ?? throw new InvalidOperationException("The Finance fixture requires an academic stage.");
			var standard = await GetRequiredService<StandardManager>().CreateAsync(stageId, "Class 8", 12, null);
			await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(standard, true);
			var division = await GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "A", 30);
			await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
			student.UpdateAcademicPlacement(fixture.YearId, student.AcademicBoardId!.Value, student.AcademicMediumId,
				stageId, standard.Id, division.Id);
			await students.UpdateAsync(student, true);
			var metadata = new FeeStructureMetadata(Guid.NewGuid(), fixture.TenantId, fixture.YearId,
				student.AcademicBoardId.Value, student.AcademicMediumId, null, "Existing school fees");
			await GetRequiredService<IRepository<FeeStructureMetadata, Guid>>().InsertAsync(metadata, true);
			var structure = new FeeStructure(Guid.NewGuid(), fixture.TenantId, metadata.Id, standard.Id, "Class 8 fees");
			await GetRequiredService<IRepository<FeeStructure, Guid>>().InsertAsync(structure, true);
			var policy = new RefundPolicy(Guid.NewGuid(), fixture.TenantId, fixture.YearId, new RefundPolicyUpdateModel(
				"Existing paid-charge refunds", "EXTERNAL-GUARD", null, RefundPolicyStatus.Active, false,
				RefundFeeHeadScope.AllFeeHeads, [], true, false, true, RefundAmountMode.ManualUpToEligible,
				null, null, null, null, RoundingMethodType.None, RefundDeductionType.None,
				null, null, null, [RefundPayoutMethod.Cash], null));
			await GetRequiredService<IRepository<RefundPolicy, Guid>>().InsertAsync(policy, true);
			var version = new FinancePolicyVersion(Guid.NewGuid(), fixture.TenantId, "refund", policy.Id,
				fixture.YearId, policy.Version, policy.PolicyName, policy.Status.ToString(),
				JsonSerializer.Serialize(policy.ToSnapshot()), "created", "Existing refund policy");
			await GetRequiredService<IRepository<FinancePolicyVersion, Guid>>().InsertAsync(version, true);
			await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().InsertAsync(new StudentFeeStructure(
				Guid.NewGuid(), fixture.TenantId, fixture.PersonId, structure.Id, fixture.YearId,
				null, null, ConcessionFeeHeadScope.EntireFeeStructure, [], refundPolicyId: policy.Id,
				refundPolicyVersionId: version.Id), true);
			var charge = await GetRequiredService<IRepository<Charge, Guid>>().GetAsync(posted.FinanceChargeId);
			typeof(Charge).GetProperty(nameof(Charge.Status))!.SetValue(charge, ChargeStatus.Approved);
			await GetRequiredService<IRepository<Charge, Guid>>().UpdateAsync(charge, true);
			var legacyRequest = new ChargeRequest(Guid.NewGuid(), fixture.TenantId, fixture.YearId,
				"Existing manual charge", fixture.FinanceCategoryId, "Previously collected school charge", 70m,
				DateTimeOffset.UtcNow.AddDays(7), ChargePriority.Medium, student.AcademicBoardId.Value,
				student.AcademicMediumId, fixture.ActorId, "{}");
			await GetRequiredService<IRepository<ChargeRequest, Guid>>().InsertAsync(legacyRequest, true);
			var legacyCharge = new Charge(Guid.NewGuid(), fixture.TenantId, fixture.YearId, legacyRequest.Id,
				legacyRequest.ChargeName, fixture.FinanceCategoryId, fixture.PersonId, student.AcademicBoardId.Value,
				student.AcademicMediumId, standard.Id, division.Id, legacyRequest.PurposeDescription, 70m,
				legacyRequest.DueDate!.Value, ChargePriority.Medium, ChargeStatus.Approved, fixture.ActorId);
			await GetRequiredService<IRepository<Charge, Guid>>().InsertAsync(legacyCharge, true);
			var number = "EXT-" + Guid.NewGuid().ToString("N")[..10];
			var collection = new FeeRecordMetadata(Guid.NewGuid(), fixture.TenantId, fixture.PersonId,
				fixture.YearId, 170d, number, FeeCollectionStatus.Paid);
			await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(collection, true);
			await GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>().InsertAsync(new(Guid.NewGuid())
			{
				TenantId = fixture.TenantId, StudentId = fixture.PersonId, ChargeId = charge.Id,
				FeeRecordMetadataId = collection.Id, Amount = 100m, PaymentDate = DateTimeOffset.UtcNow
			}, true);
			await GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>().InsertAsync(new(Guid.NewGuid())
			{
				TenantId = fixture.TenantId, StudentId = fixture.PersonId, ChargeId = legacyCharge.Id,
				FeeRecordMetadataId = collection.Id, Amount = 70m, PaymentDate = DateTimeOffset.UtcNow
			}, true);
			var receipt = new FeePaymentReceipt(Guid.NewGuid())
			{
				TenantId = fixture.TenantId, StudentId = fixture.PersonId, AcademicYearId = fixture.YearId,
				BoardId = student.AcademicBoardId.Value, EducationMediumId = student.AcademicMediumId,
				FeeCollectionTransactionId = collection.Id, ReceiptNumber = number, ReceiptDate = new DateOnly(2026, 11, 17),
				ReceiptScope = FeeReceiptScope.Charge, ReceiptKind = FeeReceiptKind.FinalCharge,
				VerificationStatus = FeeReceiptVerificationStatus.Verified, TotalPaidAmount = 170m
			};
			receipt.SetAmountBreakdown(0m, 170m);
			receipt.SetLateFeePaidAmount(0m);
			await GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().InsertAsync(receipt, true);
			await GetRequiredService<IPermissionManager>().SetAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View,
				"R", $"external-finance-{fixture.ActorId:N}", true);
			return legacyCharge.Id;
		});
}
