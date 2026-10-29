using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Finance.Mobile;
using Volo.Abp;

namespace ShikshaMitra.Controllers.Finance.Mobile;

[ApiController, Authorize, Area("app"), RemoteService(Name="Default")]
[Route("api/mobile/v1/finance/students/{studentId:guid}")]
public partial class MobileFinanceController(IMobileFinanceReadService reads) : ShikshaMitraController
{
	[HttpGet("context")]
	public async Task<Result<MobileFinanceContext>> Context(Guid studentId,[FromQuery] Guid? academicYearId,CancellationToken cancellationToken)=>Result.Success(await reads.ContextAsync(studentId,academicYearId,cancellationToken));
	[HttpGet("overview")]
	public async Task<Result<MobileFinanceOverview>> Overview(Guid studentId,[FromQuery] Guid academicYearId,CancellationToken cancellationToken)=>Result.Success(await reads.OverviewAsync(studentId,academicYearId,cancellationToken));
	[HttpGet("installments")]
	public async Task<Result<List<MobileFinanceInstallment>>> Installments(Guid studentId,[FromQuery] Guid academicYearId,CancellationToken cancellationToken)=>Result.Success(await reads.InstallmentsAsync(studentId,academicYearId,cancellationToken));
	[HttpGet("charges")]
	public async Task<Result<List<MobileFamilyCharge>>> Charges(Guid studentId,[FromQuery] Guid academicYearId,CancellationToken cancellationToken)=>Result.Success(await reads.ChargesAsync(studentId,academicYearId,cancellationToken));
	[HttpGet("payments")]
	public async Task<Result<MobileFinanceList<MobileFinancePayment>>> Payments(Guid studentId,[FromQuery] Guid academicYearId,[FromQuery] int skipCount=0,[FromQuery] int maxResultCount=20,[FromQuery] string? status=null,CancellationToken cancellationToken=default)=>Result.Success(await reads.PaymentsAsync(studentId,academicYearId,skipCount,maxResultCount,cancellationToken,status));
	[HttpGet("transactions")]
	public async Task<Result<MobileFinanceList<MobileFinanceTransaction>>> Transactions(Guid studentId,[FromQuery] Guid academicYearId,[FromQuery] int skipCount=0,[FromQuery] int maxResultCount=20,CancellationToken cancellationToken=default)=>Result.Success(await reads.TransactionsAsync(studentId,academicYearId,skipCount,maxResultCount,cancellationToken));
	[HttpGet("receipts")]
	public async Task<Result<MobileFinanceList<MobileFinanceReceipt>>> Receipts(Guid studentId,[FromQuery] Guid academicYearId,[FromQuery] int skipCount=0,[FromQuery] int maxResultCount=20,CancellationToken cancellationToken=default)=>Result.Success(await reads.ReceiptsAsync(studentId,academicYearId,skipCount,maxResultCount,cancellationToken));
	[HttpGet("requests")]
	public async Task<Result<MobileFinanceRequestPage>> Requests(Guid studentId,[FromQuery] Guid academicYearId,[FromQuery] string? kind=null,[FromQuery] int skipCount=0,[FromQuery] int maxResultCount=20,CancellationToken cancellationToken=default)=>Result.Success(await reads.RequestsAsync(studentId,academicYearId,kind,skipCount,maxResultCount,cancellationToken));
	[HttpGet("receipts/{receiptId:guid}/pdf")]
	public async Task<FileContentResult> ReceiptPdf(Guid studentId,Guid receiptId,[FromQuery] Guid academicYearId,CancellationToken cancellationToken)=>File(await reads.ReceiptPdfAsync(studentId,academicYearId,receiptId,cancellationToken),"application/pdf",$"receipt-{receiptId}.pdf");
	[HttpGet("statement/pdf")]
	public async Task<FileContentResult> Statement(Guid studentId,[FromQuery] Guid academicYearId,CancellationToken cancellationToken)=>File(await reads.StatementPdfAsync(studentId,academicYearId,cancellationToken),"application/pdf",$"statement-{studentId}.pdf");
}
