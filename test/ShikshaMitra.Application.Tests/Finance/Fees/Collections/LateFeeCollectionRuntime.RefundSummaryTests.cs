using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CsvHelper;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class LateFeeCollectionRuntime_Tests
{
	[Theory]
	[InlineData(3000, 500, 0)]
	[InlineData(5000, 1500, 1500)]
	[InlineData(1000, 0, 0)]
	public async Task Summary_Preserves_Gross_Paid_Fees_And_Reports_Refunds_Separately(int paid, int refunded, int concession)
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, student, _, dueId, _) = await SeedStudentWithOverdueDueFeeAsync(0, true, enableLateFee: false);
			var due = await _dueFeeRepo.GetAsync(dueId);
			due.ApplyPayment(paid);
			if (concession > 0) due.ApplyConcession(concession);
			await _dueFeeRepo.UpdateAsync(due, autoSave: true);
			var before = await GetSummaryRowAsync(year, student);
			before.PaidFees.ShouldBe(paid);
			if (refunded > 0) due.ApplyRefund(refunded);
			await _dueFeeRepo.UpdateAsync(due, autoSave: true);

			var after = await GetSummaryRowAsync(year, student);
			after.PaidFees.ShouldBe(paid);
			after.PendingFees.ShouldBe(before.PendingFees);
			after.Status.ShouldBe(before.Status);
			ReadSummaryRefund(after).ShouldBe(refunded);

			var payer = await _studentRepo.GetAsync(student);
			var input = new GetFeeCollectionSummaryInput
			{
				AcademicYearId = year, BoardId = payer.AcademicBoardId!.Value,
				EducationMediumId = payer.AcademicMediumId,
				Filters = $$"""[{"column":"refundAmount","type":"equals","value":{{refunded}}}]""",
				SortBy = "refundAmount", SortOrder = "desc",
			};
			(await _feeCollections.GetSummaryAsync(input)).Items.Single().StudentId.ShouldBe(student);

			var export = new FeeCollectionExportRequestDto
			{
				AcademicYearId = year, BoardId = input.BoardId, EducationMediumId = input.EducationMediumId,
				Format = FeeCollectionExportFormat.Csv,
			};
			using (var csv = new CsvReader(new StringReader(System.Text.Encoding.UTF8.GetString(
				await _feeCollections.ExportAsync(export))), CultureInfo.InvariantCulture))
			{
				csv.Read().ShouldBeTrue(); csv.ReadHeader();
				csv.HeaderRecord!.Skip(4).Take(5).ShouldBe(new[]
					{ "Total Fees", "Total Concession Amount", "Refund Amount", "Paid Fees", "Pending Fees" });
				csv.Read().ShouldBeTrue();
				csv.GetField<double>("Paid Fees").ShouldBe(paid);
				csv.GetField<decimal>("Refund Amount").ShouldBe(refunded);
			}
			export.Format = FeeCollectionExportFormat.Excel;
			using var workbook = new XLWorkbook(new MemoryStream(await _feeCollections.ExportAsync(export)));
			var sheet = workbook.Worksheets.First();
			sheet.Cell(1, 7).GetString().ShouldBe("Refund Amount");
			sheet.Cell(2, 7).GetValue<decimal>().ShouldBe(refunded);
			sheet.Cell(2, 8).GetDouble().ShouldBe(paid);

			if (refunded > 0)
			{
				due.ReverseRefund(refunded);
				await _dueFeeRepo.UpdateAsync(due, autoSave: true);
				var returned = await GetSummaryRowAsync(year, student);
				returned.PaidFees.ShouldBe(paid);
				ReadSummaryRefund(returned).ShouldBe(0m);
			}
		}
	}

	private static decimal ReadSummaryRefund(FeeCollectionSummaryItemDto row)
	{
		var property = typeof(FeeCollectionSummaryItemDto).GetProperty("RefundAmount");
		property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(decimal));
		return (decimal)property.GetValue(row)!;
	}
}
