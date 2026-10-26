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
	public async Task<Result<List<MobileFinanceCharge>>> Charges(Guid studentId,[FromQuery] Guid academicYearId,CancellationToken cancellationToken)=>Result.Success(await reads.ChargesAsync(studentId,academicYearId,cancellationToken));
	[HttpGet("payments")]
	public async Task<Result<MobileFinancePage<MobileFinancePayment>>> Payments(Guid studentId,[FromQuery] Guid academicYearId,[FromQuery] int page=1,[FromQuery] int pageSize=20,[FromQuery] string? status=null,CancellationToken cancellationToken=default)=>Result.Success(await reads.PaymentsAsync(studentId,academicYearId,page,pageSize,cancellationToken,status));
	[HttpGet("receipts")]
	public async Task<Result<MobileFinancePage<MobileFinanceReceipt>>> Receipts(Guid studentId,[FromQuery] Guid academicYearId,[FromQuery] int page=1,[FromQuery] int pageSize=20,CancellationToken cancellationToken=default)=>Result.Success(await reads.ReceiptsAsync(studentId,academicYearId,page,pageSize,cancellationToken));
	[HttpGet("requests")]
	public async Task<Result<MobileFinancePage<MobileFinanceRequest>>> Requests(Guid studentId,[FromQuery] Guid academicYearId,[FromQuery] string? kind=null,[FromQuery] int page=1,[FromQuery] int pageSize=20,CancellationToken cancellationToken=default)=>Result.Success(await reads.RequestsAsync(studentId,academicYearId,kind,page,pageSize,cancellationToken));
	[HttpGet("receipts/{receiptId:guid}/pdf")]
	public async Task<FileContentResult> ReceiptPdf(Guid studentId,Guid receiptId,[FromQuery] Guid academicYearId,CancellationToken cancellationToken)=>File(await reads.ReceiptPdfAsync(studentId,academicYearId,receiptId,cancellationToken),"application/pdf",$"receipt-{receiptId}.pdf");
	[HttpGet("statement/pdf")]
	public async Task<FileContentResult> Statement(Guid studentId,[FromQuery] Guid academicYearId,CancellationToken cancellationToken)=>File(await reads.StatementPdfAsync(studentId,academicYearId,cancellationToken),"application/pdf",$"statement-{studentId}.pdf");
}
