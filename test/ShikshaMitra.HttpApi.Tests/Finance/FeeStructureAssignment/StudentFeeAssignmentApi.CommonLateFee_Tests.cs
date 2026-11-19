using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.FeeStructureAssignment;

public sealed record CommonLateFeeHttpScenario(Guid StudentId, Guid AcademicYearId, Guid StructureId, Guid PolicyId, Guid[] FeeHeadIds, Guid[] DueFeeIds, Guid IncomeCategoryId);
public sealed record CommonLateFeeHttpVerification(CommonLateFeeHttpScenario Scenario, int DueCount, int HeadCount,
	double GrossLateFee, double PartialPaid, double PartialPending, double FinalPaid, double FinalPending, int RepeatedReads);

public partial class StudentFeeAssignmentApi_Tests
{
	// Also used by the disposable WebApplicationFactory browser bridge.
	public async Task<CommonLateFeeHttpScenario> SeedCommonLateFeeBrowserScenarioAsync(Guid? collectionPolicyId = null)
	{
		var (input, heads) = await CreateCommonLateFeeFixtureAsync(collectionPolicyId);
		await PreviewAndAssignAsync(input);
		var form = await GetAndReadAsync<FeeCollectFormDto>($"/api/fee-collections/{input.StudentId}/collect-form?academicYearId={input.AcademicYearId}");
		var dues = form.Ledger.SelectMany(x => x.Items).ToList();
		dues.Count.ShouldBe(4);
		dues.Select(x => x.FeeHeadId).Distinct().Count().ShouldBe(2);
		form.Charges.Single(x => x.SourceType == "LateFee").PendingAmount.ShouldBe(125d);
		var policy = await GetAndReadAsync<CollectionPolicyDto>($"/api/finance/collection-policies/{form.CollectionPolicyId!.Value}");
		return new(input.StudentId, input.AcademicYearId, input.StandardFeeStructureId, policy.Id, heads,
			dues.Select(x => x.DueFeeId).ToArray(), policy.LateFeeIncomeCategoryId!.Value);
	}

	private async Task<(AssignStudentFeeStructureDto Input, Guid[] Heads)> CreateCommonLateFeeFixtureAsync(Guid? collectionPolicyId)
	{
		// Reuse existing synthetic enrollment/account/scope prerequisites without
		// modifying the shared fixture or its unrelated policy scenarios.
		var (input, availableHeads) = await CreateFixtureAsync(optionalHeads: true, useCurrentAcademicContext: true);
		var heads = availableHeads.Take(2).ToArray();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var userId = Guid.Parse(TestAuthHandler.TestUserId);
		Guid metadataId, standardId, incomeCategoryId;
		var candidate = new StandardFeeStructureRequest { Name = $"Common-{Guid.NewGuid():N}" };
		using (var services = Factory.Services.CreateScope())
		using (services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId))
		using (var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var provider = services.ServiceProvider;
			var student = await provider.GetRequiredService<IStudentRepository>().GetForFeeAssignmentAsync(input.StudentId);
			student.ShouldNotBeNull();
			var year = await provider.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(input.AcademicYearId);
			standardId = student.StandardId!.Value;
			incomeCategoryId = (await provider.GetRequiredService<IRepository<FeeHead, Guid>>().GetAsync(heads[0])).IncomeCategoryId;
			var metadata = new FeeStructureMetadata(Guid.NewGuid(), tenantId, year.Id, student.AcademicBoardId!.Value,
				student.AcademicMediumId, null, candidate.Name);
			await provider.GetRequiredService<IRepository<FeeStructureMetadata, Guid>>().InsertAsync(metadata, autoSave: true);
			metadataId = metadata.Id;
			var today = provider.GetRequiredService<IClock>().Now.Date;
			var term = new PaymentTerm(Guid.NewGuid(), tenantId, year.Id, student.AcademicBoardId!.Value, candidate.Name,
				$"T-{Guid.NewGuid():N}"[..16], null, false, HolidayShiftingMethod.None, DemandGenerationRule.OnDueDate, null, InitialDemandStatus.Draft);
			var version = new PaymentTermVersion(Guid.NewGuid(), tenantId, term.Id, 1, PaymentTermVersionStatus.Draft,
				HolidayShiftingMethod.None, DemandGenerationRule.OnDueDate, null, null, null);
			version.SetInstallments([
				new PaymentTermInstallment(Guid.NewGuid(), version.Id, 1, "Earlier month", year.StartDate, today.AddDays(-11), today.AddDays(-40), today.AddDays(-40), 50m, true, false),
				new PaymentTermInstallment(Guid.NewGuid(), version.Id, 2, "Later month", today.AddDays(-10), year.EndDate, today.AddDays(-10), today.AddDays(-10), 50m, true, false)
			]);
			version.Publish(year.StartDate, userId);
			term.AddVersion(version);
			await provider.GetRequiredService<IRepository<PaymentTerm, Guid>>().InsertAsync(term, autoSave: true);
			for (var index = 0; index < heads.Length; index++) candidate.FeeHeads.Add(new()
			{
				FeeHeadId = heads[index], Amount = index == 0 ? 12_000m : 6_000m,
				PaymentTermId = term.Id, PaymentTermVersionId = version.Id
			});
			await uow.CompleteAsync();
		}
		var policy = collectionPolicyId.HasValue
			? await GetAndReadAsync<CollectionPolicyDto>($"/api/finance/collection-policies/{collectionPolicyId.Value}")
			: await PostAndReadAsync<CollectionPolicyDto>("/api/finance/collection-policies", new CreateUpdateCollectionPolicyDto
		{
			AcademicYearId = input.AcademicYearId, PolicyName = candidate.Name, PolicyCode = $"CP-{Guid.NewGuid():N}"[..16],
			Status = "Active", EnableLateFee = true, FlatAmount = 125m, LateFeeType = "Flat", GracePeriodDays = 0,
			AllocationBasis = "Installment",
			LateFeeIncomeCategoryId = incomeCategoryId, LateFeeApplyOnFeeHeads = [], AllowOnlineCollection = true,
			FeeHeadOverrides = [new() { FeeHeadId = heads[1], OverrideMode = "NoLateFee" }]
		});
		policy.AcademicYearId.ShouldBe(input.AcademicYearId, "The browser-created policy must belong to the scenario academic year.");
		policy.ApplyLateFeeToAllFeeHeads.ShouldBeTrue(); policy.LateFeeApplyOnFeeHeads.ShouldBeEmpty(); policy.FeeHeadOverrides.ShouldBeEmpty();
		candidate.CollectionPolicyId = policy.Id;
		var draft = await PostAndReadAsync<FeeStructureCreateResponse>($"/api/fee-structures?feeStructureMetadataId={metadataId}",
			new StandardFeeStructureSaveRequest { StandardId = standardId, StandardFeeStructures = candidate });
		var published = await PostAndReadAsync<FeeStructureCreateResponse>($"/api/fee-structures/{draft.Id}/publish",
			new FeeStructurePublishRequest { ConcurrencyStamp = draft.ConcurrencyStamp });
		input.StandardFeeStructureId = published.Id;
		return (input, heads);
	}

	public async Task<CommonLateFeeHttpVerification> VerifyCommonLateFeeBrowserScenarioAsync(Guid? collectionPolicyId = null)
	{
		var scenario = await SeedCommonLateFeeBrowserScenarioAsync(collectionPolicyId);
		var policy = await GetAndReadAsync<CollectionPolicyDto>($"/api/finance/collection-policies/{scenario.PolicyId}");
		policy.ApplyLateFeeToAllFeeHeads.ShouldBeTrue();
		policy.LateFeeApplyOnFeeHeads.ShouldBeEmpty();
		var simulation = await PostAndReadAsync<CollectionPolicySimulateResponseDto>($"/api/finance/collection-policies/{scenario.PolicyId}/simulate",
			new CollectionPolicySimulateRequestDto { DaysLate = 40, OutstandingAmount = 18_000m, FeeHeadId = scenario.FeeHeadIds[1] });
		simulation.LateFee.ShouldBe(125m);
		var url = $"/api/fee-collections/{scenario.StudentId}/collect-form?academicYearId={scenario.AcademicYearId}";
		var initial = await GetAndReadAsync<FeeCollectFormDto>(url);
		var charge = initial.Charges.Single(x => x.SourceType == "LateFee");
		charge.Name.ShouldBe("Late Fee");
		var due = initial.Ledger.SelectMany(x => x.Items).First(x => x.DueFeeId == scenario.DueFeeIds[0]);
		// Select principal and the generated charge separately, as the screen does.
		// Automatic allocation exhausts each selected principal before a later charge.
		await PostAndReadAsync<CollectFeeOutput>("/api/fee-collections", new CollectFeeInput
		{
			StudentId = scenario.StudentId, AcademicYearId = scenario.AcademicYearId, IdempotencyKey = Guid.NewGuid().ToString("N"),
			DueFeePayments = [new() { DueFeeId = due.DueFeeId, FeeHeadId = due.FeeHeadId, Amount = 1000d }],
			AutomaticAllocation = SelectCommonLateFeeRows(initial, dueFeeId: due.DueFeeId),
			Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 1000d }]
		});
		var afterPrincipal = await GetAndReadAsync<FeeCollectFormDto>(url);
		afterPrincipal.Ledger.SelectMany(x => x.Items).Sum(x => x.Due).ShouldBe(17_000d);
		charge = afterPrincipal.Charges.Single(x => x.SourceType == "LateFee");
		charge.Amount.ShouldBe(125d); charge.PendingAmount.ShouldBe(125d);
		await PostAndReadAsync<CollectFeeOutput>("/api/fee-collections", new CollectFeeInput
		{
			StudentId = scenario.StudentId, AcademicYearId = scenario.AcademicYearId, IdempotencyKey = Guid.NewGuid().ToString("N"),
			LateFeePayments = [new() { DueFeeId = charge.SourceId, Amount = 50d }],
			AutomaticAllocation = SelectCommonLateFeeRows(afterPrincipal, chargeId: charge.ChargeId),
			Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 50d }]
		});
		double partialPaid = 0d, partialPending = 0d;
		var repeatedReads = 0;
		for (var attempt = 0; attempt < 2; attempt++)
		{
			var partial = (await GetAndReadAsync<FeeCollectFormDto>(url)).Charges.Single(x => x.SourceType == "LateFee");
			partial.Amount.ShouldBe(125d); partial.PaidAmount.ShouldBe(50d); partial.PendingAmount.ShouldBe(75d);
			partialPaid = partial.PaidAmount; partialPending = partial.PendingAmount; repeatedReads++;
		}
		var beforeSettlement = await GetAndReadAsync<FeeCollectFormDto>(url);
		charge = beforeSettlement.Charges.Single(x => x.SourceType == "LateFee");
		await PostAndReadAsync<CollectFeeOutput>("/api/fee-collections", new CollectFeeInput
		{
			StudentId = scenario.StudentId, AcademicYearId = scenario.AcademicYearId, IdempotencyKey = Guid.NewGuid().ToString("N"),
			LateFeePayments = [new() { DueFeeId = charge.SourceId, Amount = 75d }],
			AutomaticAllocation = SelectCommonLateFeeRows(beforeSettlement, chargeId: charge.ChargeId),
			Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 75d }]
		});
		double finalPaid = 0d, finalPending = 0d, finalGross = 0d;
		for (var attempt = 0; attempt < 2; attempt++)
		{
			var paid = (await GetAndReadAsync<FeeCollectFormDto>(url)).Charges.Single(x => x.SourceType == "LateFee");
			paid.Amount.ShouldBe(125d); paid.PaidAmount.ShouldBe(125d); paid.PendingAmount.ShouldBe(0d);
			finalGross = paid.Amount; finalPaid = paid.PaidAmount; finalPending = paid.PendingAmount; repeatedReads++;
		}
		return new(scenario, scenario.DueFeeIds.Length, scenario.FeeHeadIds.Length, finalGross,
			partialPaid, partialPending, finalPaid, finalPending, repeatedReads);
	}

	private static AutomaticCollectionSelectionDto? SelectCommonLateFeeRows(FeeCollectFormDto form, Guid? dueFeeId = null, Guid? chargeId = null)
	{
		if (form.AllocationBasis == "Legacy") return null;
		form.AllocationRevision.ShouldNotBeNullOrWhiteSpace();
		return new()
		{
			Revision = form.AllocationRevision!,
			DueFeeIds = dueFeeId.HasValue ? [dueFeeId.Value] : [],
			ChargeIds = chargeId.HasValue ? [chargeId.Value] : []
		};
	}
}
