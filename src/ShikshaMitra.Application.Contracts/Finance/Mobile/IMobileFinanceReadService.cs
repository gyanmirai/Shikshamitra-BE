using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Mobile;

public interface IMobileFinanceReadService : IApplicationService
{
	Task<MobileFinanceContext> ContextAsync(Guid studentId, Guid? academicYearId = null, CancellationToken cancellationToken = default);
	Task<MobileFinanceOverview> OverviewAsync(Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default);
	Task<List<MobileFinanceInstallment>> InstallmentsAsync(Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default);
	Task<List<MobileFinanceCharge>> ChargesAsync(Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default);
	Task<MobileFinancePage<MobileFinancePayment>> PaymentsAsync(Guid studentId, Guid academicYearId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default, string? status = null);
	Task<MobileFinancePage<MobileFinanceReceipt>> ReceiptsAsync(Guid studentId, Guid academicYearId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
	Task<MobileFinancePage<MobileFinanceRequest>> RequestsAsync(Guid studentId, Guid academicYearId, string? kind = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
	Task<byte[]> ReceiptPdfAsync(Guid studentId, Guid academicYearId, Guid receiptId, CancellationToken cancellationToken = default);
	Task<byte[]> StatementPdfAsync(Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default);
}
