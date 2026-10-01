using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using System.Collections.Generic;
using System.IO;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class FeeCollectionSummaryQueryService
{
	private static byte[] BuildCsv(List<FeeCollectionSummaryItemDto> rows)
	{
		var config = new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
		{
			HasHeaderRecord = true,
		};

		using var ms     = new MemoryStream();
		using var writer = new StreamWriter(ms, leaveOpen: true);
		using var csv    = new CsvWriter(writer, config);

		csv.WriteField("Student Name");
		csv.WriteField("GR Number");
		csv.WriteField("Roll Number");
		csv.WriteField("Class / Division");
		csv.WriteField("Total Fees");
		csv.WriteField("Paid Fees");
		csv.WriteField("Pending Fees");
		csv.WriteField("Overdue Fees");
		csv.WriteField("Total Charges");
		csv.WriteField("Pending Charges");
		csv.WriteField("Status");
		csv.NextRecord();

		foreach (var r in rows)
		{
			csv.WriteField(r.StudentName);
			csv.WriteField(r.GrNumber);
			csv.WriteField(r.RollNumber);
			csv.WriteField(r.StandardDivision);
			csv.WriteField(r.TotalFees);
			csv.WriteField(r.PaidFees);
			csv.WriteField(r.PendingFees);
			csv.WriteField(r.OverdueFees);
			csv.WriteField(r.TotalCharges);
			csv.WriteField(r.PendingCharges);
			csv.WriteField(GetExportStatusLabel(r.Status));
			csv.NextRecord();
		}

		writer.Flush();
		return ms.ToArray();
	}

	private static byte[] BuildExcel(List<FeeCollectionSummaryItemDto> rows)
	{
		using var workbook = new XLWorkbook();
		var sheet = workbook.Worksheets.Add("Fee Collection");

		var headers = new[]
		{
			"Student Name", "GR Number", "Roll Number", "Class / Division",
			"Total Fees", "Paid Fees", "Pending Fees", "Overdue Fees",
			"TotalCharges", "Pending Charges", "Status",
		};

		for (var col = 0; col < headers.Length; col++)
		{
			var cell = sheet.Cell(1, col + 1);
			cell.Value = headers[col];
			cell.Style.Font.Bold = true;
			cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
		}

		for (var i = 0; i < rows.Count; i++)
		{
			var r   = rows[i];
			var row = i + 2;
			sheet.Cell(row, 1).Value  = r.StudentName;
			sheet.Cell(row, 2).Value  = r.GrNumber;
			sheet.Cell(row, 3).Value  = r.RollNumber;
			sheet.Cell(row, 4).Value  = r.StandardDivision;
			sheet.Cell(row, 5).Value  = r.TotalFees;
			sheet.Cell(row, 6).Value  = r.PaidFees;
			sheet.Cell(row, 7).Value  = r.PendingFees;
			sheet.Cell(row, 8).Value  = r.OverdueFees;
			sheet.Cell(row, 9).Value  = r.TotalCharges;
			sheet.Cell(row, 10).Value = r.PendingCharges;
			sheet.Cell(row, 11).Value = GetExportStatusLabel(r.Status);

			if (r.Status == FeeCollectionStatus.Overdue)
				sheet.Row(row).Style.Fill.BackgroundColor = XLColor.LightSalmon;
		}

		sheet.Columns().AdjustToContents();

		using var ms = new MemoryStream();
		workbook.SaveAs(ms);
		return ms.ToArray();
	}

	private static string GetExportStatusLabel(FeeCollectionStatus status)
		=> status == FeeCollectionStatus.NoFeeAssigned ? "No Fee Assigned" : status.ToString();
}
