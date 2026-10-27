using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Common.PDF;
using Permissions = ShikshaMitra.Finance.Mobile.MobileFinancePermissions;

namespace ShikshaMitra.Finance.Mobile;

public partial class MobileFinanceReadService
{
	[Authorize(Permissions.DownloadStatement)]
	public async Task<byte[]> StatementPdfAsync(Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default)
	{
		var context=await access.RequireAsync(studentId,academicYearId,permission:Permissions.DownloadStatement,cancellationToken:cancellationToken);
		var snapshot=await ReadSnapshotAsync(studentId,academicYearId,cancellationToken);
		var overview=snapshot.Overview;
		var installments=await BuildInstallmentsAsync(studentId,academicYearId,snapshot.Dues,cancellationToken);
		var chargeRows=await access.IsGrantedAsync(Permissions.ViewCharges) ? snapshot.Charges : [];
		var payments=await PaymentDtosAsync((await transactions.GetListAsync(x=>x.StudentId==studentId&&x.AcademicYearId==academicYearId,cancellationToken:cancellationToken))
			.OrderBy(x=>x.CreationTime).ThenBy(x=>x.Id).ToList(),studentId,academicYearId,cancellationToken);
		QuestPDF.Settings.License=LicenseType.Community; PortablePdfFonts.EnsureRegistered();
		return Document.Create(document=>document.Page(page=>
		{
			page.Size(PageSizes.A4);page.Margin(28);page.DefaultTextStyle(x=>x.FontSize(10).FontFamily(PortablePdfFonts.DefaultFamily,PortablePdfFonts.GujaratiFamily));
			page.Header().Column(column=>
			{
				column.Item().Text("Official fee statement").FontSize(20).SemiBold();
				column.Item().Text(context.StudentName);
				column.Item().Text($"Academic year: {context.AcademicYears.Single(x=>x.Id==academicYearId).Name} | Currency: INR");
				column.Item().Text($"As of {overview.AsOf:yyyy-MM-dd} | Student reference: {studentId}");
			});
			page.Content().PaddingVertical(12).Column(column=>
			{
				foreach(var (label,amount) in new[]{("Billed principal",overview.BilledAmount),("Verified payments (all scopes)",overview.VerifiedPaidAmount),("Payments awaiting verification",overview.PendingVerificationAmount),
					("Billed principal outstanding",overview.OutstandingAmount),("Future scheduled principal",overview.FutureScheduledAmount),("Charge outstanding",overview.ChargeOutstandingAmount),
					("Late fee outstanding",overview.LateFeeAmount),("Concession",overview.ConcessionAmount),("Charge and late fee waivers",overview.WaiverAmount),("Noncash adjustment",overview.AdjustmentAmount),("Refunded principal",overview.RefundedAmount)})
					column.Item().Row(row=>{row.RelativeItem().Text(label);row.ConstantItem(100).AlignRight().Text(amount);});
				column.Item().PaddingTop(12).Text("Installments").SemiBold();
				column.Item().Table(table=>
				{
					table.ColumnsDefinition(cols=>{cols.RelativeColumn(3);cols.RelativeColumn(2);cols.RelativeColumn();cols.RelativeColumn();cols.RelativeColumn();});
					table.Header(header=>{foreach(var text in new[]{"Fee / installment","Due / status","Amount","Paid","Outstanding"})header.Cell().BorderBottom(1).Padding(3).Text(text).SemiBold();});
					foreach(var item in installments)
						foreach(var text in new[]{$"{item.FeeHeadName} / {item.Label}",$"{item.DueDate:yyyy-MM-dd} / {item.Status}",item.Amount,item.PaidAmount,item.OutstandingAmount})table.Cell().Padding(3).Text(text);
				});
				if(chargeRows.Count>0)
				{
					column.Item().PaddingTop(12).Text("Additional charges").SemiBold();
					foreach(var item in chargeRows)column.Item().Text($"{item.Name} — outstanding INR {MobileFinanceProjection.Money(item.Outstanding)}");
				}
				if(payments.Count>0)
				{
					column.Item().PaddingTop(12).Text("Payment history").SemiBold();
					column.Item().Table(table=>
					{
						table.ColumnsDefinition(cols=>{cols.RelativeColumn();cols.RelativeColumn(2);cols.RelativeColumn(2);cols.RelativeColumn();});
						table.Header(header=>{foreach(var text in new[]{"Date","Receipt / method","Status","Amount"})header.Cell().BorderBottom(1).Padding(3).Text(text).SemiBold();});
						foreach(var item in payments)
							foreach(var text in new[]{$"{item.Date:yyyy-MM-dd}",string.Join(", ",item.Receipts.Select(x=>x.ReceiptNumber))+" / "+string.Join(", ",item.PaymentMethods),item.Status,item.Amount})table.Cell().Padding(3).Text(text);
					});
				}
				column.Item().PaddingTop(12).Text("Pending verification is shown separately. Future installments are not billed outstanding. This statement is not a payment receipt.").FontSize(9);
			});
			page.Footer().AlignCenter().Text(text=>{text.Span("Page ");text.CurrentPageNumber();text.Span(" / ");text.TotalPages();});
		})).GeneratePdf();
	}
}
