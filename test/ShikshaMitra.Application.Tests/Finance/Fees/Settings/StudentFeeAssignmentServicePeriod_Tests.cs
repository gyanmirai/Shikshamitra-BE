using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
	[Theory]
	[InlineData("2025-10-01", "2026-03-31", 1, "Second half")]
	[InlineData("2025-04-01", "2025-09-30", 1, "First half")]
	[InlineData("2025-09-30", "2025-10-01", 2, "First half")]
	public async Task Manual_Service_Period_Allocates_Only_To_Overlapping_Terms(string start, string end, int count, string firstLabel)
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var input = await ReadyAssignmentAsync(amount: 12000m);
		var service = GetRequiredService<IStudentFeeStructureAppService>();
		var head = (await service.PreviewAsync(input)).Heads.Single();
		input.ServicePeriods = [new() { FeeHeadId = head.FeeHeadId, Source = "ManualOverride",
			StartDate = DateTime.Parse(start), EndDate = DateTime.Parse(end), Reason = "Selected service period" }];
		var preview = await service.PreviewAsync(input);
		preview.Lines.Count.ShouldBe(count);
		preview.Lines.OrderBy(x => x.Sequence).First().InstallmentLabel.ShouldContain(firstLabel);
		preview.ProratedTotal.ShouldBe(12000m); // Full-charge pricing is unchanged.
		preview.Lines.Sum(x => x.AllocationPercentage).ShouldBe(100m);
		input.PreviewToken = preview.PreviewToken;
		var committed = await service.AssignAsync(input);
		var rows = await GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(x => x.StudentId == input.StudentId);
		rows.Count.ShouldBe(count);
		rows.Sum(x => x.OriginalAmount).ShouldBe(12000m);
		(await service.GetByStudentIdAsync(input.StudentId, input.AcademicYearId)).Assignment!.Lines.Count.ShouldBe(count);
		(await service.AssignAsync(input)).AssignmentId.ShouldBe(committed.AssignmentId);
	}

	[Fact]
	public async Task Late_Joining_Proration_Total_Is_Allocated_To_Eligible_Terms_Only()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var input = await ReadyAssignmentAsync(amount: 12000m);
		var structures = GetRequiredService<IRepository<FeeStructure, Guid>>();
		var structure = await structures.GetAsync(input.StandardFeeStructureId);
		var head = structure.FeeStructureDetails.Single();
		var policy = new ProrationPolicySnapshot(Guid.NewGuid(), input.AcademicYearId, "Daily", "DAILY", ProrationPolicyStatus.Active, false, 1,
			[new(head.FeeHeadId, ProrationRuleType.Daily, ProrationRoundingMode.Currency, null, null)]);
		var rows = await GetRequiredService<DueFeeManager>().GenerateForStudentAsync(input.StudentId, structure,
			await _academicYearRepo.GetAsync(input.AcademicYearId), [], prorationPolicy: policy,
			prorationPolicyVersionId: Guid.NewGuid(), chargeStartDate: new(2025, 10, 1), chargeEndDate: new(2026, 3, 31));
		rows.Count.ShouldBe(1);
		rows.Single().InstallmentName.ShouldBe("Second half");
		rows.Sum(x => x.OriginalAmount).ShouldBe(5984m); // ceil(12000 * 182 / 365)
		rows.Sum(x => x.ScheduledAmount).ShouldBe(12000m);
		rows.Single().AllocationPercentage.ShouldBe(100m);
	}

	[Theory]
	[InlineData(20, 30, 50, 37.5, 62.5, 4500, 7500)]
	[InlineData(10, 30, 60, 33.333333, 66.666667, 3999, 8001)]
	public async Task Eligible_Terms_Retain_Relative_Weights_And_Published_Dates(decimal oldWeight, decimal secondWeight,
		decimal lastWeight, decimal expectedSecondWeight, decimal expectedLastWeight, decimal secondAmount, decimal lastAmount)
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var input = await ReadyAssignmentAsync(amount: 12000m);
		using var tracking = GetRequiredService<IRepository<FeeStructure, Guid>>().DisableTracking();
		var structure = await GetRequiredService<IRepository<FeeStructure, Guid>>().GetAsync(input.StandardFeeStructureId);
		var metadata = await _metadataRepo.GetAsync(structure.FeeStructureMetadataId);
		var term = new PaymentTerm(Guid.NewGuid(), TestTenantId, input.AcademicYearId, metadata.AcademicBoardId!.Value,
			"Weighted terms", "WEIGHTED", null, false, HolidayShiftingMethod.None, DemandGenerationRule.OnDueDate, null, InitialDemandStatus.Draft);
		var version = new PaymentTermVersion(Guid.NewGuid(), TestTenantId, term.Id, 1, PaymentTermVersionStatus.Draft,
			HolidayShiftingMethod.None, DemandGenerationRule.OnDueDate, null, null, null);
		version.SetInstallments([
			new(Guid.NewGuid(), version.Id, 1, "Past", new(2025, 4, 1), new(2025, 6, 30), new(2025, 4, 10), new(2025, 4, 10), oldWeight, true, false),
			new(Guid.NewGuid(), version.Id, 2, "Current", new(2025, 7, 1), new(2025, 9, 30), new(2025, 7, 10), new(2025, 7, 10), secondWeight, true, false),
			new(Guid.NewGuid(), version.Id, 3, "Future", new(2025, 10, 1), new(2026, 3, 31), new(2025, 10, 10), new(2025, 10, 10), lastWeight, true, false)]);
		version.Publish(new(2025, 4, 1), TestUserId);
		term.AddVersion(version);
		await GetRequiredService<IRepository<PaymentTerm, Guid>>().InsertAsync(term, autoSave: true);
		structure.FeeStructureDetails.Single().AttachPaymentTerm(term.Id, version.Id);
		var rows = (await GetRequiredService<DueFeeManager>().GenerateForStudentAsync(input.StudentId, structure,
			await _academicYearRepo.GetAsync(input.AcademicYearId), [], chargeStartDate: new(2025, 7, 1), chargeEndDate: new(2026, 3, 31)))
			.OrderBy(x => x.InstallmentSequence).ToList();
		rows.Select(x => x.AllocationPercentage).ShouldBe(new decimal?[] { expectedSecondWeight, expectedLastWeight });
		rows.Select(x => x.OriginalAmount).ShouldBe(new[] { secondAmount, lastAmount });
		rows.Sum(x => x.ScheduledAmount).ShouldBe(12000m);
		rows.Select(x => x.InstallmentSequence).ShouldBe(new int?[] { 2, 3 });
		for (var i = 0; i < rows.Count; i++)
		{
			var published = version.Installments.Single(x => x.Sequence == i + 2);
			rows[i].PaymentTermInstallmentId.ShouldBe(published.Id);
			rows[i].DueDate.ShouldBe(published.DueDate);
			rows[i].DemandDate.ShouldBe(published.DemandDate);
		}
	}

	[Fact]
	public async Task Service_Period_Without_An_Overlapping_Term_Is_Rejected_Without_Writes()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var input = await ReadyAssignmentAsync(amount: 12000m);
		using var tracking = GetRequiredService<IRepository<FeeStructure, Guid>>().DisableTracking();
		var structure = await GetRequiredService<IRepository<FeeStructure, Guid>>().GetAsync(input.StandardFeeStructureId);
		var head = structure.FeeStructureDetails.Single();
		var term = await GetRequiredService<IRepository<PaymentTerm, Guid>>().GetAsync(head.PaymentTermId!.Value);
		var installments = term.Versions.Single(x => x.Id == head.PaymentTermVersionId).Installments.OrderBy(x => x.Sequence).ToList();
		head.AttachPaymentTermWithOverrides(term.Id, head.PaymentTermVersionId!.Value, [
			new(Guid.NewGuid(), TestTenantId, head.Id, installments[0].Id, 1, "First", coverageEndDate: new(2025, 4, 30)),
			new(Guid.NewGuid(), TestTenantId, head.Id, installments[1].Id, 2, "Last", coverageStartDate: new(2026, 3, 1), dueDate: new(2026, 3, 10))]);
		var year = await _academicYearRepo.GetAsync(input.AcademicYearId);
		var error = await Should.ThrowAsync<BusinessException>(() => GetRequiredService<DueFeeManager>().GenerateForStudentAsync(
			input.StudentId, structure, year, [],
			chargeStartDate: new(2025, 11, 1), chargeEndDate: new(2025, 11, 30)));
		error.Code.ShouldBe(AssignmentErrorCodes.ConfigurationInvalid);
		(await GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(x => x.StudentId == input.StudentId)).ShouldBeEmpty();
	}
}
