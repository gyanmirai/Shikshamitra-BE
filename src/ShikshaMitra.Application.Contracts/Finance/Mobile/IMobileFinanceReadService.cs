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
	Task<List<MobileFamilyCharge>> ChargesAsync(Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default);
	Task<MobileChargePage> ChargesPageAsync(Guid studentId, Guid academicYearId, int page = 1, int pageSize = 20, string filter = "all", string? search = null, Guid? chargeCategoryId = null, CancellationToken cancellationToken = default);
	Task<MobileChargeDetail> ChargeAsync(Guid studentId, Guid academicYearId, Guid chargeId, CancellationToken cancellationToken = default);
	Task<MobileChargePaymentPage> ChargePaymentsAsync(Guid studentId, Guid academicYearId, Guid chargeId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
	Task<MobileFinanceList<MobileFinancePayment>> PaymentsAsync(Guid studentId, Guid academicYearId, int skipCount = 0, int maxResultCount = 20, CancellationToken cancellationToken = default, string? status = null);
	Task<MobileFinanceList<MobileFinanceTransaction>> TransactionsAsync(Guid studentId, Guid academicYearId, int skipCount = 0, int maxResultCount = 20, CancellationToken cancellationToken = default);
	Task<MobileFinanceList<MobileFinanceReceipt>> ReceiptsAsync(Guid studentId, Guid academicYearId, int skipCount = 0, int maxResultCount = 20, CancellationToken cancellationToken = default);
	Task<MobileFinanceRequestPage> RequestsAsync(Guid studentId, Guid academicYearId, string? kind = null, int skipCount = 0, int maxResultCount = 20, CancellationToken cancellationToken = default);
	Task<byte[]> ReceiptPdfAsync(Guid studentId, Guid academicYearId, Guid receiptId, CancellationToken cancellationToken = default);
	Task<byte[]> StatementPdfAsync(Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default);
}
