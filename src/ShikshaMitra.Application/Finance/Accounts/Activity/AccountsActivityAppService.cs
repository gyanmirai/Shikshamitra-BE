using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Students;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Accounts.Activity;

/// <summary>
/// Read-only source activity. A final receipt or refund subledger entry is evidence
/// of a Fees action, not a General Accounts journal posting.
/// </summary>
[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Activity.View)]
public class AccountsActivityAppService(
    IRepository<FeePaymentReceipt, Guid> receiptRepository,
    IRepository<FeeRecordMetadata, Guid> collectionRepository,
    IRepository<FeeRefundAccountingEntry, Guid> refundEntryRepository,
    IRepository<FeeRefundRequest, Guid> refundRepository,
    IReadOnlyRepository<Student, Guid> studentRepository,
    FinanceScopeAccessService scopeAccessService) : ApplicationService, IAccountsActivityAppService
{
    public async Task<AccountsActivityPageDto> GetListAsync(AccountsActivityInput input)
    {
        if (input.AcademicYearId == Guid.Empty)
            throw new UserFriendlyException("Select an academic year to view Accounts activity.");
        var source = input.Source?.Trim().ToLowerInvariant();
        if (source is not (null or "collection" or "refund"))
            throw new UserFriendlyException("Source must be collection or refund.");
        var search = input.Search?.Trim();
        if (search?.Length > 100)
            throw new UserFriendlyException("Search must be 100 characters or fewer.");

        var page = Math.Clamp(input.Page, 1, 1000);
        var pageSize = Math.Clamp(input.PageSize, 1, 100);
        var window = checked(page * pageSize);
        var scopedStudentIds = await scopeAccessService.GetScopedStudentIdsQueryAsync();
        var rows = new List<AccountsActivityRowDto>();
        var total = 0;

        if (source is null or "collection")
        {
            var receipts = await receiptRepository.GetQueryableAsync();
            var collections = await collectionRepository.GetQueryableAsync();
            var query = from receipt in receipts
                        join collection in collections on receipt.FeeCollectionTransactionId equals collection.Id
                        where receipt.AcademicYearId == input.AcademicYearId
                            && scopedStudentIds.Contains(receipt.StudentId)
                            && (receipt.ReceiptKind == FeeReceiptKind.FinalFee ||
                                receipt.ReceiptKind == FeeReceiptKind.FinalCharge)
                        select new { receipt, collection };
            if (!string.IsNullOrEmpty(search))
                query = query.Where(x => x.receipt.ReceiptNumber.Contains(search) ||
                    x.collection.ReceiptNumber.Contains(search));
            total += await AsyncExecuter.CountAsync(query);
            var selected = await AsyncExecuter.ToListAsync(query
                .OrderByDescending(x => x.receipt.CreationTime).ThenByDescending(x => x.receipt.Id)
                .Take(window).Select(x => new
                {
                    x.receipt.Id, x.receipt.StudentId, x.receipt.ReceiptNumber,
                    x.receipt.TotalPaidAmount, x.receipt.CreationTime,
                    x.receipt.FeeCollectionTransactionId, x.collection.Status
                }));
            var canOpen = await AuthorizationService.IsGrantedAsync(
                ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View);
            rows.AddRange(selected.Select(x => new AccountsActivityRowDto
            {
                Id = x.Id,
                SourceId = x.FeeCollectionTransactionId,
                StudentId = x.StudentId,
                Source = "Collection",
                Reference = x.ReceiptNumber,
                SourceStatus = x.Status == FeeCollectionStatus.Reversed ? "Reversed" : "Accepted",
                Amount = Convert.ToDecimal(x.TotalPaidAmount),
                OccurredAt = new DateTimeOffset(DateTime.SpecifyKind(x.CreationTime, DateTimeKind.Utc)),
                CanOpenSource = canOpen
            }));
        }

        if (source is null or "refund")
        {
            var entries = await refundEntryRepository.GetQueryableAsync();
            var refunds = await refundRepository.GetQueryableAsync();
            var query = from entry in entries
                        join refund in refunds on entry.RefundRequestId equals refund.Id
                        where refund.AcademicYearId == input.AcademicYearId
                            && scopedStudentIds.Contains(refund.StudentId)
                        select new { entry, refund };
            if (!string.IsNullOrEmpty(search))
                query = query.Where(x => x.refund.VoucherNumber.Contains(search) ||
                    (x.refund.OriginalReceiptNumber != null &&
                     x.refund.OriginalReceiptNumber.Contains(search)));
            total += await AsyncExecuter.CountAsync(query);
            var selected = await AsyncExecuter.ToListAsync(query
                .OrderByDescending(x => x.entry.PostedAt).ThenByDescending(x => x.entry.Id)
                .Take(window).Select(x => new
                {
                    x.entry.Id, x.entry.RefundRequestId, x.entry.Amount,
                    x.entry.IsCorrection, x.entry.PostedAt,
                    x.refund.StudentId, x.refund.VoucherNumber
                }));
            var canOpen = await AuthorizationService.IsGrantedAsync(
                ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View);
            rows.AddRange(selected.Select(x => new AccountsActivityRowDto
            {
                Id = x.Id,
                SourceId = x.RefundRequestId,
                StudentId = x.StudentId,
                Source = "Refund",
                Reference = x.VoucherNumber,
                SourceStatus = x.IsCorrection ? "Returned" : "Paid",
                Amount = x.IsCorrection ? x.Amount : -x.Amount,
                OccurredAt = x.PostedAt,
                CanOpenSource = canOpen
            }));
        }

        var pageRows = rows.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var studentIds = pageRows.Select(x => x.StudentId).Distinct().ToList();
        if (studentIds.Count > 0)
        {
            var students = await studentRepository.GetQueryableAsync();
            var names = await AsyncExecuter.ToListAsync(students.Where(x => studentIds.Contains(x.Id))
                .Select(x => new { x.Id, x.FirstName, x.MiddleName, x.LastName }));
            var byId = names.ToDictionary(x => x.Id, x => string.Join(" ",
                new[] { x.FirstName, x.MiddleName, x.LastName }
                    .Where(value => !string.IsNullOrWhiteSpace(value))));
            foreach (var row in pageRows)
                row.StudentName = byId.GetValueOrDefault(row.StudentId) ?? string.Empty;
        }

        return new AccountsActivityPageDto
        {
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            Items = pageRows
        };
    }
}
