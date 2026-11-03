using System;
using System.Linq;
using System.IO;
using System.Globalization;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CsvHelper;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class LateFeeCollectionRuntime_Tests
{
	[Theory]
	[InlineData(false, 0, 5000d, 5000d, 0d, 0d, 0d)]
	[InlineData(false, 0, 1000d, 200d, 300d, 800d, 500d)]
	[InlineData(true, 250, 5000d, 5000d, 0d, 0d, 0d)]
	[InlineData(true, 50, 1000d, 200d, 300d, 800d, 500d)]
	public async Task Summary_Charges_Are_Net_Of_Applied_Waivers_While_Details_Preserve_Gross_And_Paid(
		bool systemCharge, int daysOverdue, double gross, double waived, double paid, double expectedTotal, double expectedPending)
	{
		using (_currentTenant.Change(TestTenantId))
		{
			// The pinned policy charges 20/day: system assessments match their original due dates;
			// manual-only cases use a due dated today to avoid an incidental system charge.
			var (year, student, _, due, _) = await SeedStudentWithOverdueDueFeeAsync(daysOverdue, true);
			var chargeId = due;
			if (systemCharge)
			{
				var assignment = (await _studentFeeStructureRepo.GetListAsync(x =>
					x.StudentId == student && x.AcademicYearId == year)).Single();
				var dueFee = await _dueFeeRepo.GetAsync(due);
				dueFee.PreserveLateFeeAssessment((decimal)gross, DateTimeOffset.UtcNow,
					assignment.CollectionPolicyVersionId!.Value, "Assessed late fee");
				await _dueFeeRepo.UpdateAsync(dueFee, autoSave: true);
				await GetRequiredService<IRepository<LateFeeWaiverApplication, Guid>>().InsertAsync(
					new LateFeeWaiverApplication(Guid.NewGuid(), TestTenantId, student, due, assignment.CollectionPolicyId!.Value, year,
						(decimal)gross, (decimal)waived, (decimal)waived, "Applied waiver", false, FakeAdminUserId), autoSave: true);
			}
			else
			{
				var payer = await _studentRepo.GetAsync(student);
				var charge = new Charge(Guid.NewGuid(), TestTenantId, year, Guid.NewGuid(), "Summary charge", Guid.NewGuid(),
					student, payer.AcademicBoardId!.Value, payer.AcademicMediumId, Guid.NewGuid(), Guid.NewGuid(),
					"Summary waiver regression", (decimal)gross, DateTimeOffset.UtcNow, ChargePriority.Medium,
					ChargeStatus.Pending, Guid.NewGuid());
				charge.Approve(Guid.NewGuid());
				chargeId = charge.Id;
				await GetRequiredService<IRepository<Charge, Guid>>().InsertAsync(charge, autoSave: true);
				await GetRequiredService<IRepository<ChargeWaiverApplication, Guid>>().InsertAsync(
					new ChargeWaiverApplication(Guid.NewGuid(), TestTenantId, student, charge.Id, year, (decimal)gross,
						(decimal)waived, "Applied waiver", FakeAdminUserId, Guid.NewGuid().ToString("N")), autoSave: true);
			}
			if (paid > 0d)
			{
				var metadata = new FeeRecordMetadata(Guid.NewGuid(), TestTenantId, student, year, paid,
					$"SUMMARY-{Guid.NewGuid():N}", FeeCollectionStatus.Paid);
				await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(metadata, autoSave: true);
				if (systemCharge)
					await GetRequiredService<IRepository<LateFeeCollectionAllocation, Guid>>().InsertAsync(
						new LateFeeCollectionAllocation(Guid.NewGuid(), TestTenantId, metadata.Id, due, paid), autoSave: true);
				else
					await GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>().InsertAsync(
						new ChargeCollectionRecord(Guid.NewGuid()) { TenantId = TestTenantId, StudentId = student,
							ChargeId = chargeId, FeeRecordMetadataId = metadata.Id, Amount = paid, PaymentDate = DateTimeOffset.UtcNow }, autoSave: true);
			}
			var row = await GetSummaryRowAsync(year, student);
			row.TotalCharges.ShouldBe(expectedTotal);
			ReadPaidCharges(row).ShouldBe((decimal)paid);
			row.PendingCharges.ShouldBe(expectedPending);
			var detail = (await _feeCollections.GetCollectFormAsync(student, year)).Charges.Single();
			detail.Amount.ShouldBe(gross);
			detail.PaidAmount.ShouldBe(paid);
			detail.WaivedAmount.ShouldBe(waived);
			detail.PendingAmount.ShouldBe(expectedPending);
			var payerInfo = await _studentRepo.GetAsync(student);
			var csv = System.Text.Encoding.UTF8.GetString(await _feeCollections.ExportAsync(new FeeCollectionExportRequestDto
			{
				AcademicYearId = year, BoardId = payerInfo.AcademicBoardId!.Value,
				EducationMediumId = payerInfo.AcademicMediumId, Format = FeeCollectionExportFormat.Csv,
			}));
			using (var reader = new CsvReader(new StringReader(csv), CultureInfo.InvariantCulture))
			{
				reader.Read().ShouldBeTrue(); reader.ReadHeader();
				reader.HeaderRecord!.Skip(8).Take(3).ToArray().ShouldBe(new[] { "Total Charges", "Paid Charges", "Pending Charges" });
				reader.Read().ShouldBeTrue();
				reader.GetField<double>("Total Charges").ShouldBe(expectedTotal);
				reader.GetField<decimal>("Paid Charges").ShouldBe((decimal)paid);
				reader.GetField<double>("Pending Charges").ShouldBe(expectedPending);
			}
			var excel = await _feeCollections.ExportAsync(new FeeCollectionExportRequestDto
			{
				AcademicYearId = year, BoardId = payerInfo.AcademicBoardId!.Value,
				EducationMediumId = payerInfo.AcademicMediumId, Format = FeeCollectionExportFormat.Excel,
			});
			using var workbook = new XLWorkbook(new MemoryStream(excel));
			var sheet = workbook.Worksheets.First();
			sheet.Cell(1, 9).GetString().ShouldBe("TotalCharges");
			sheet.Cell(1, 10).GetString().ShouldBe("Paid Charges");
			sheet.Cell(1, 11).GetString().ShouldBe("Pending Charges");
			sheet.Cell(2, 9).GetDouble().ShouldBe(expectedTotal);
			sheet.Cell(2, 10).GetValue<decimal>().ShouldBe((decimal)paid);
			sheet.Cell(2, 11).GetDouble().ShouldBe(expectedPending);
		}
	}

	[Fact]
	public async Task Paid_Charges_Combine_Manual_And_System_Allocations_Exclude_Reversals_And_Drive_Filtering_And_Sorting()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, firstId, _, dueId, _) = await SeedStudentWithOverdueDueFeeAsync(1, true);
			var first = await _studentRepo.GetAsync(firstId);
			var second = new Student(Guid.NewGuid(), TestTenantId, "Second", null, "Payer", year,
				first.AcademicBoardId!.Value, first.AcademicMediumId, first.AcademicMediumStageId!.Value,
				grNo: 2, rollNo: 2, status: StudentLifeCycleStatusEnum.Enrolled);
			await _studentRepo.InsertAsync(second, autoSave: true);
			await SeedSummaryManualChargeAsync(first, year, 300.25d, FeeCollectionStatus.Paid, reversed: 100d);
			await SeedSummaryManualChargeAsync(second, year, 100.50d, FeeCollectionStatus.InReconciliation);
			var metadata = new FeeRecordMetadata(Guid.NewGuid(), TestTenantId, firstId, year, 20d,
				$"SUMMARY-LATE-{Guid.NewGuid():N}", FeeCollectionStatus.Paid);
			await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(metadata, autoSave: true);
			await GetRequiredService<IRepository<LateFeeCollectionAllocation, Guid>>().InsertAsync(
				new LateFeeCollectionAllocation(Guid.NewGuid(), TestTenantId, metadata.Id, dueId, 20d), autoSave: true);
			var input = new GetFeeCollectionSummaryInput
			{
				AcademicYearId = year, BoardId = first.AcademicBoardId.Value, EducationMediumId = first.AcademicMediumId,
				MaxResultCount = 100, SortBy = "paidCharges", SortOrder = "desc"
			};
			var descending = await _feeCollections.GetSummaryAsync(input);
			descending.Items.Select(x => x.StudentId).ShouldBe(new Guid?[] { firstId, second.Id });
			ReadPaidCharges(descending.Items[0]).ShouldBe(320.25m);
			ReadPaidCharges(descending.Items[1]).ShouldBe(100.50m);
			descending.Items[0].TotalCharges.ShouldBe(1020d);
			descending.Items[0].PendingCharges.ShouldBe(699.75d);
			descending.Items[1].PendingCharges.ShouldBe(899.50d);
			descending.Items.ShouldAllBe(x => x.PaidFees == 0d);
			input.SortOrder = "asc";
			(await _feeCollections.GetSummaryAsync(input)).Items.Select(x => x.StudentId).ShouldBe(new Guid?[] { second.Id, firstId });
			input.SortBy = null; input.Sorting = "PaidCharges desc";
			(await _feeCollections.GetSummaryAsync(input)).Items.Select(x => x.StudentId).ShouldBe(new Guid?[] { firstId, second.Id });
			input.Filters = "[{\"column\":\"paidCharges\",\"type\":\"greaterThan\",\"value\":200}]";
			var filtered = await _feeCollections.GetSummaryAsync(input);
			filtered.TotalCount.ShouldBe(1); filtered.Items.Single().StudentId.ShouldBe(firstId);
		}
	}

	private static decimal ReadPaidCharges(FeeCollectionSummaryItemDto row)
	{
		var contract = System.Text.Json.JsonSerializer.SerializeToElement(row);
		contract.TryGetProperty("PaidCharges", out var paid).ShouldBeTrue("The summary must expose collected charges separately from net assessed and pending charges.");
		return paid.GetDecimal();
	}

	private async Task SeedSummaryManualChargeAsync(Student payer, Guid year, double paid, FeeCollectionStatus status, double reversed = 0d)
	{
		var charge = new Charge(Guid.NewGuid(), TestTenantId, year, Guid.NewGuid(), "Paid summary charge", Guid.NewGuid(),
			payer.Id, payer.AcademicBoardId!.Value, payer.AcademicMediumId, Guid.NewGuid(), Guid.NewGuid(),
			"Paid summary regression", 1000m, DateTimeOffset.UtcNow, ChargePriority.Medium, ChargeStatus.Pending, Guid.NewGuid());
		charge.Approve(Guid.NewGuid());
		await GetRequiredService<IRepository<Charge, Guid>>().InsertAsync(charge, autoSave: true);
		foreach (var (amount, paymentStatus) in new[] { (paid, status), (reversed, FeeCollectionStatus.Reversed) })
		{
			if (amount <= 0d) continue;
			var metadata = new FeeRecordMetadata(Guid.NewGuid(), TestTenantId, payer.Id, year, amount,
				$"SUMMARY-MANUAL-{Guid.NewGuid():N}", paymentStatus);
			await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(metadata, autoSave: true);
			await GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>().InsertAsync(
				new ChargeCollectionRecord(Guid.NewGuid()) { TenantId = TestTenantId, StudentId = payer.Id,
					ChargeId = charge.Id, FeeRecordMetadataId = metadata.Id, Amount = amount, PaymentDate = DateTimeOffset.UtcNow }, autoSave: true);
		}
	}
}
