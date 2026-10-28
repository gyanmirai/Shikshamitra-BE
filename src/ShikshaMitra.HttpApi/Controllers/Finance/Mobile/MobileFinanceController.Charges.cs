using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Finance.Mobile;

namespace ShikshaMitra.Controllers.Finance.Mobile;

public partial class MobileFinanceController
{
	[Authorize(MobileFinancePermissions.ViewCharges), HttpGet("charges/page")]
	public async Task<Result<MobileChargePage>> ChargesPage(Guid studentId, [FromQuery] Guid academicYearId,
		[FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string filter = "all",
		[FromQuery] string? search = null, [FromQuery] Guid? categoryId = null, CancellationToken cancellationToken = default) =>
		Result.Success(await reads.ChargesPageAsync(studentId, academicYearId, page, pageSize, filter, search, categoryId, cancellationToken));
	[Authorize(MobileFinancePermissions.ViewCharges), HttpGet("charges/{chargeId:guid}")]
	public async Task<Result<MobileChargeDetail>> Charge(Guid studentId, Guid chargeId, [FromQuery] Guid academicYearId,
		CancellationToken cancellationToken = default) => Result.Success(await reads.ChargeAsync(studentId, academicYearId, chargeId, cancellationToken));
	[Authorize(MobileFinancePermissions.ViewCharges), HttpGet("charges/{chargeId:guid}/payments")]
	public async Task<Result<MobileChargePaymentPage>> ChargePayments(Guid studentId, Guid chargeId, [FromQuery] Guid academicYearId,
		[FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
		Result.Success(await reads.ChargePaymentsAsync(studentId, academicYearId, chargeId, page, pageSize, cancellationToken));
}
