using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Common.PDF;
using ShikshaMitra.Finance.Fees.Collections;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Uow;
namespace ShikshaMitra.Finance.Accounts.BankActivity;
public partial class BankAccountActivityAppService
{
	public const int ExportRowLimit = 5000;
	private static readonly IReadOnlyDictionary<string, PropertyInfo> ExportFields = typeof(BankActivityRow).GetProperties()
	 .Where(x => x.Name is not (nameof(BankActivityRow.SourceId) or nameof(BankActivityRow.CashSourceId) or nameof(BankActivityRow.Key) or nameof(BankActivityRow.DepositId) or nameof(BankActivityRow.CorrectsKey) or nameof(BankActivityRow.CanOpenSource)))
	 .ToDictionary(x => char.ToLowerInvariant(x.Name[0]) + x.Name[1..]);
	[UnitOfWork(IsDisabled = true)]
	public async Task<IRemoteStreamContent> ExportAsync(Guid id, BankActivityExportRequest input)
	{
		if (input.Query == null || input.Format is not ("excel" or "pdf") || input.Columns == null || input.Columns.Count > 20 ||
		 input.Columns.Any(x => x == null || !ExportFields.ContainsKey(x.Id) || string.IsNullOrWhiteSpace(x.Label) || x.Label.Length > 100) || input.Columns.Select(x => x.Id).Distinct().Count() != input.Columns.Count)
			throw new BusinessException(BankActivityErrorCodes.InvalidQuery);
		if (input.Columns.Count == 0) input.Columns = [new("occurredAt", "Activity date"), new("reference", "Reference"), new("source", "Source"), new("incoming", "Incoming (INR)"), new("outgoing", "Outgoing (INR)")];
		using var snapshot = units.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.RepeatableRead }, requiresNew: true);
		var context = await ContextAsync(id, input.Query.AcademicYearId);
		var page = await PageAsync(context, input.Query);
		if (page.TotalCount > ExportRowLimit) throw new BusinessException(BankActivityErrorCodes.ExportLimit).WithData("MaximumRows", ExportRowLimit);
		var table = BankActivityQueryRules.Validate(input.Query);
		var query = BankActivityQueryRules.Filter(context.Facts, input.Query, table);
		var facts = await AsyncExecuter.ToListAsync(BankActivityQueryRules.Order(query, table).Take(ExportRowLimit + 1));
		if (facts.Count > ExportRowLimit || facts.Count != page.TotalCount) throw new BusinessException(BankActivityErrorCodes.ExportLimit);
		var rows = new List<BankActivityRow>(); foreach (var fact in facts) rows.Add(await MapAsync(fact));
		var bytes = input.Format == "excel" ? Excel(page, rows, input.Columns) : Pdf(page, rows, input.Columns);
		await snapshot.CompleteAsync();
		return new RemoteStreamContent(new MemoryStream(bytes), $"bank-activity-{page.GeneratedAt:yyyyMMdd-HHmmss}.{(input.Format == "excel" ? "xlsx" : "pdf")}",
		 input.Format == "excel" ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "application/pdf");
	}
	private static object Value(BankActivityRow row, string column) => column switch
	{
		"paymentMethod" => Enum.GetName(typeof(PaymentMethodEnum), row.PaymentMethod) ?? "Unknown",
		"recognitionStatus" => Enum.GetName(typeof(BankActivityRecognitionStatus), row.RecognitionStatus) ?? "Unknown",
		"bankConfirmationStatus" => Enum.GetName(typeof(BankActivityConfirmationStatus), row.BankConfirmationStatus) ?? "Unknown",
		_ => ExportFields[column].GetValue(row) ?? ""
	};
	private static string Text(object value) => value is DateTimeOffset date ? date.ToString("O", CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
	private static byte[] Excel(BankActivityPage page, List<BankActivityRow> rows, List<BankActivityExportColumn> columns)
	{
		using var workbook = new XLWorkbook(); var context = workbook.AddWorksheet("Context");
		var metadata = new (string Label, object Value)[]{("Account",$"{page.Account.BankName} {page.Account.MaskedAccountNumber}"),("Academic year",page.AcademicYearName),("Currency","INR"),
   ("From",page.From.ToString("O")),("To exclusive",page.ToExclusive.ToString("O")),("Coverage",page.Coverage.Message),("Incoming",page.Summary.Incoming),
   ("Outgoing",page.Summary.Outgoing),("Net",page.Summary.Net),("Pending incoming",page.Summary.PendingIncoming),("In transit",page.Summary.InTransit),("Matching rows",page.TotalCount)};
		for (var i = 0; i < metadata.Length; i++) { context.Cell(i + 1, 1).Value = metadata[i].Label; Set(context.Cell(i + 1, 2), metadata[i].Value); }
		context.Column(1).Width = 22; context.Column(2).Width = 95; context.Column(2).Style.Alignment.WrapText = true;
		var sheet = workbook.AddWorksheet("Activity");
		for (var column = 0; column < columns.Count; column++) sheet.Cell(1, column + 1).Value = columns[column].Label;
		for (var index = 0; index < rows.Count; index++) for (var column = 0; column < columns.Count; column++) Set(sheet.Cell(index + 2, column + 1), Value(rows[index], columns[column].Id));
		sheet.Row(1).Style.Font.Bold = true; sheet.SheetView.FreezeRows(1); sheet.Columns().Width = 24;
		using var stream = new MemoryStream(); workbook.SaveAs(stream); return stream.ToArray();
	}
	private static void Set(IXLCell cell, object value)
	{ if (value is decimal money) { cell.Value = money; cell.Style.NumberFormat.Format = "0.00"; } else if (value is int count) cell.Value = count; else cell.Value = Text(value); }
	private static byte[] Pdf(BankActivityPage page, List<BankActivityRow> rows, List<BankActivityExportColumn> columns)
	{
		QuestPDF.Settings.License = LicenseType.Community; PortablePdfFonts.EnsureRegistered();
		return Document.Create(document => document.Page(p =>
		{
			p.Size(PageSizes.A4.Landscape()); p.Margin(20); p.DefaultTextStyle(x => x.FontSize(8).FontFamily(PortablePdfFonts.DefaultFamily, PortablePdfFonts.GujaratiFamily));
			p.Header().Column(c =>
			{
				c.Item().Text($"{page.Account.BankName} {page.Account.MaskedAccountNumber} — {page.AcademicYearName}").Bold(); c.Item().Text(page.Coverage.Message);
				c.Item().Text($"{page.From:O} to {page.ToExclusive:O} (exclusive) | INR | Incoming {page.Summary.Incoming:0.00} | Outgoing {page.Summary.Outgoing:0.00} | Net {page.Summary.Net:0.00} | Pending {page.Summary.PendingIncoming:0.00} | Transit {page.Summary.InTransit:0.00}");
			});
			p.Content().PaddingTop(10).Table(t =>
			{
				t.ColumnsDefinition(c => { foreach (var column in columns) c.RelativeColumn(); });
				t.Header(h => { foreach (var column in columns) h.Cell().BorderBottom(1).Padding(3).Text(column.Label).Bold(); });
				foreach (var row in rows) foreach (var column in columns) t.Cell().BorderBottom(.25f).Padding(3).Text(Text(Value(row, column.Id)));
			});
			p.Footer().AlignCenter().Text(t => { t.Span("Visible activity • "); t.CurrentPageNumber(); });
		})).GeneratePdf();
	}
}
