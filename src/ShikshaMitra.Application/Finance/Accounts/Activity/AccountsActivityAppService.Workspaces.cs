using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Common;
using Volo.Abp;

namespace ShikshaMitra.Finance.Accounts.Activity;

public partial class AccountsActivityAppService
{
    private sealed class WorkspaceRecord
    {
        public Guid Id { get; set; }
        public Guid SourceId { get; set; }
        public Guid? StudentId { get; set; }
        public Guid? FinanceFeeAccountId { get; set; }
        public Guid? CategoryId { get; set; }
        public Guid? BankAccountId { get; set; }
        public string StudentName { get; set; } = "";
        public string GrNumber { get; set; } = "";
        public string ClassDivision { get; set; } = "";
        public string Counterparty { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public string BankLabel { get; set; } = "";
        public string Reference { get; set; } = "";
        public string Source { get; set; } = "";
        public string Status { get; set; } = "";
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
    }

    private async Task<AccountsActivityPageDto> GetWorkspaceListAsync(AccountsActivityInput input)
    {
        var income = input.Direction == "income";
        if (!income && input.Direction != "expense") throw new BusinessException(AccountsRegisterErrorCodes.InvalidFilter);
        await CheckWorkspacePermissionAsync(income);
        var source = input.Source?.Trim().ToLowerInvariant();
        if (source != null && (income ? source is not ("income" or "collection") : source is not ("expense" or "refund")))
            throw new BusinessException(AccountsRegisterErrorCodes.InvalidFilter);
        if (input.AcademicYearId == Guid.Empty || input.Page < 1 || input.Page > 1000 || input.PageSize is < 1 or > 100 || input.Search?.Length > 100)
            throw new BusinessException(AccountsRegisterErrorCodes.InvalidFilter);
        var sortBy = input.SortBy ?? "occurredAt";
        var tableQuery = FinanceTableQuery.Parse(input.Filters, null, sortBy,
            input.SortDescending ? "desc" : "asc", AccountsRegisterTable.Columns);
        if (tableQuery.Filters.Any(f => f.Column == "source" && (f.Type != "equals" ||
                !(income ? new[] { "Income", "Collection" } : new[] { "Expense", "Refund" }).Contains(f.Value.GetString()))) ||
            tableQuery.Filters.Any(f => f.Column == "sourceStatus" && (f.Type != "equals" ||
                !(income ? new[] { "Received", "Reversed" } : new[] { "Paid", "Returned", "Awaiting payout" }).Contains(f.Value.GetString()))))
            throw new BusinessException(AccountsRegisterErrorCodes.InvalidFilter);
        var rowFilters = WorkspaceFilters(tableQuery.Filters.Where(f => f.Column != "counterparty"));
        var result = new AccountsActivityPageDto { Page = input.Page, PageSize = input.PageSize };
        var scopedAccounts = await scopeAccessService.GetScopedFeeAccountIdsQueryAsync();
        var accounts = await accountRepository.GetQueryableAsync();
        if (!await scopeAccessService.IsAcademicContextAccessibleAsync(input.AcademicYearId, null, null, null, null) &&
            !await AsyncExecuter.AnyAsync(accounts.Where(a => a.AcademicYearId == input.AcademicYearId && scopedAccounts.Contains(a.Id)))) return result;
        var year = await academicYears.GetAsync(input.AcademicYearId);
        var period = AccountsRegisterPeriod.Resolve(year.StartDate, year.EndDate, input.Month, input.Year);
        var profiles = await WorkspaceStudentsAsync(input.AcademicYearId);
        var scopedStudents = await scopeAccessService.GetScopedStudentIdsQueryAsync();
        var students = await studentRepository.GetQueryableAsync();
        var transactions = await scopeAccessService.FilterPooledCashTransactionsAsync(
            await transactionRepository.GetQueryableAsync(), input.AcademicYearId);
        var categories = await categoryRepository.GetQueryableAsync();
        var type = income ? TransactionType.Income : TransactionType.Expense;
        // Suppress receipt/refund fallbacks using ALL routing evidence, including None.
        var automatic = transactions.Where(x => x.SourceKey != null);
        transactions = AccountsRecognitionQuery.ExcludeNone(transactions, categories);
        Guid[]? partyFilterIds = null;
        Guid[] partySearchIds = [];
        if (tableQuery.Filters.Any(f => f.Column == "counterparty") || !string.IsNullOrWhiteSpace(input.Search))
        {
            // Counterparty is historical JSON evidence. Read only the scoped manual
            // identifiers/evidence when this field is searched, before counting/paging.
            var manualParties = await AsyncExecuter.ToListAsync(transactions.Where(t =>
                t.AcademicYearId == input.AcademicYearId && t.TransactionType == type && t.IsConfirmed &&
                t.SourceKey == null && t.PaymentDate >= period.StartUtc && t.PaymentDate < period.EndUtc &&
                ((t.FinanceFeeAccountId.HasValue && scopedAccounts.Contains(t.FinanceFeeAccountId!.Value)) ||
                 (!t.FinanceFeeAccountId.HasValue && (t.StudentId == null || scopedStudents.Contains(t.StudentId!.Value)))))
                .Select(t => new { t.Id, t.ExtraProperties }));
            var partyRows = manualParties.Select(t => new WorkspaceRecord
            {
                Id = t.Id, Counterparty = t.ExtraProperties.TryGetValue("Counterparty", out var name) ? name?.ToString() ?? "" : ""
            }).AsQueryable();
            if (tableQuery.Filters.Any(f => f.Column == "counterparty"))
                partyFilterIds = WorkspaceFilters(tableQuery.Filters.Where(f => f.Column == "counterparty"))
                    .Apply(partyRows, WorkspaceFields).Select(x => x.Id).ToArray();
            var partyTerm = input.Search?.Trim();
            if (!string.IsNullOrEmpty(partyTerm))
                partySearchIds = partyRows.AsEnumerable().Where(x => x.Counterparty.Contains(partyTerm,
                    StringComparison.OrdinalIgnoreCase)).Select(x => x.Id).ToArray();
        }
        var queries = new List<IQueryable<WorkspaceRecord>>();
        var historicalRows = new List<WorkspaceRecord>();
        queries.Add(from t in transactions
                    join category in categories on t.CategoryId equals category.Id into categoryGroup
                    from c in categoryGroup.DefaultIfEmpty()
                    join s in students on t.StudentId equals (Guid?)s.Id into studentGroup
                    from s in studentGroup.DefaultIfEmpty()
                    join a in accounts on t.FinanceFeeAccountId equals (Guid?)a.Id into accountGroup
                    from a in accountGroup.DefaultIfEmpty()
                    where t.AcademicYearId == input.AcademicYearId && t.TransactionType == type && t.IsConfirmed &&
                        ((t.FinanceFeeAccountId.HasValue && scopedAccounts.Contains(t.FinanceFeeAccountId!.Value)) ||
                         (!t.FinanceFeeAccountId.HasValue && (t.StudentId == null || scopedStudents.Contains(t.StudentId!.Value))))
                    select new WorkspaceRecord
                    {
                        Id = t.Id, SourceId = t.SourceId ?? t.Id, StudentId = t.StudentId, FinanceFeeAccountId = t.FinanceFeeAccountId,
                        StudentName = a != null ? a.ApplicantName : s == null ? t.Name : ((s.FirstName ?? "") + " " + (s.MiddleName == null || s.MiddleName == "" ? "" : s.MiddleName + " ") + (s.LastName ?? "")).Trim(),
                        CategoryId = t.CategoryId, CategoryName = t.CategoryNameSnapshot ?? (c == null ? "Unavailable category" : c.Name),
                        BankAccountId = t.BankAccountId, BankLabel = t.BankLabelSnapshot ?? "",
                        Reference = t.Name, Amount = t.Amount, Date = t.PaymentDate,
                        Source = t.SourceKey == null ? (income ? "Income" : "Expense") : (income ? "Collection" : "Refund"),
                        Status = t.Amount < 0 ? (income ? "Reversed" : "Returned") : (income ? "Received" : "Paid")
                    });
        if (income)
        {
            var receipts = await receiptRepository.GetQueryableAsync();
            var collections = await collectionRepository.GetQueryableAsync();
            queries.Add(from r in receipts
                        join c in collections on r.FeeCollectionTransactionId equals c.Id
                        join s in students on r.StudentId equals (Guid?)s.Id into studentGroup
                        from s in studentGroup.DefaultIfEmpty()
                        join a in accounts on r.FinanceFeeAccountId equals (Guid?)a.Id into accountGroup
                        from a in accountGroup.DefaultIfEmpty()
                        where r.AcademicYearId == input.AcademicYearId &&
                            ((r.FinanceFeeAccountId.HasValue && scopedAccounts.Contains(r.FinanceFeeAccountId!.Value)) ||
                             (!r.FinanceFeeAccountId.HasValue && r.StudentId.HasValue && scopedStudents.Contains(r.StudentId!.Value))) &&
                            !automatic.Any(t => t.SourceId == c.Id) &&
                            (c.Status == FeeCollectionStatus.Paid || c.Status == FeeCollectionStatus.Partial) &&
                            (r.ReceiptKind == FeeReceiptKind.FinalFee || r.ReceiptKind == FeeReceiptKind.FinalCharge)
                        select new WorkspaceRecord
                        {
                            Id = r.Id, SourceId = c.Id, StudentId = r.StudentId, FinanceFeeAccountId = r.FinanceFeeAccountId,
                            StudentName = a != null ? a.ApplicantName : s == null ? "" : ((s.FirstName ?? "") + " " + (s.MiddleName == null || s.MiddleName == "" ? "" : s.MiddleName + " ") + (s.LastName ?? "")).Trim(),
                            CategoryId = null, CategoryName = "", BankAccountId = null, BankLabel = "",
                            Reference = r.ReceiptNumber, Amount = (decimal)r.TotalPaidAmount,
                            Date = r.CreationTime, Source = "Collection", Status = "Received"
                        });
        }
        else
        {
            var refunds = AccountsRecognitionQuery.ExcludeNone(await refundRepository.GetQueryableAsync(), categories);
            var entries = await refundEntryRepository.GetQueryableAsync();
            // Approval only reserves refundable money. Expenses begin with paid evidence.
            // Historical subledger timestamps are DateTimeOffset; materialize only the
            // already tenant/year/student-scoped fallback slice for SQLite/PostgreSQL parity.
            var legacy = await AsyncExecuter.ToListAsync(from e in entries
                join r in refunds on e.RefundRequestId equals r.Id
                join s in students on r.StudentId equals (Guid?)s.Id into studentGroup
                from s in studentGroup.DefaultIfEmpty()
                join a in accounts on r.FinanceFeeAccountId equals (Guid?)a.Id into accountGroup
                from a in accountGroup.DefaultIfEmpty()
                where r.AcademicYearId == input.AcademicYearId &&
                    ((r.FinanceFeeAccountId.HasValue && scopedAccounts.Contains(r.FinanceFeeAccountId!.Value)) ||
                     (!r.FinanceFeeAccountId.HasValue && r.StudentId.HasValue && scopedStudents.Contains(r.StudentId!.Value))) &&
                    !automatic.Any(t => t.SourceId == r.Id)
                select new { e.Id, e.RefundRequestId, e.Amount, e.IsCorrection, e.PostedAt, e.BankAccountId,
                    r.StudentId, r.FinanceFeeAccountId, r.VoucherNumber, r.ExpenseCategoryId, r.ExpenseCategoryName, r.PayingBankLabel,
                    StudentName = a != null ? a.ApplicantName : s == null ? "" : ((s.FirstName ?? "") + " " + (s.MiddleName == null || s.MiddleName == "" ? "" : s.MiddleName + " ") + (s.LastName ?? "")).Trim() });
            historicalRows.AddRange(legacy.Select(x => new WorkspaceRecord
            {
                Id = x.Id, SourceId = x.RefundRequestId, StudentId = x.StudentId, FinanceFeeAccountId = x.FinanceFeeAccountId,
                StudentName = x.StudentName,
                CategoryId = x.ExpenseCategoryId, CategoryName = x.ExpenseCategoryName ?? "",
                BankAccountId = x.BankAccountId, BankLabel = x.PayingBankLabel ?? "",
                Reference = x.VoucherNumber, Amount = x.IsCorrection ? -x.Amount : x.Amount,
                Date = x.PostedAt.UtcDateTime, Source = "Refund", Status = x.IsCorrection ? "Returned" : "Paid"
            }));
        }
        var rows = new List<WorkspaceRecord>();
        var options = new List<AccountsCategoryOptionDto>();
        if (historicalRows.Count > 0)
        {
            var historicalStudentIds = historicalRows.Where(x => x.StudentId.HasValue).Select(x => x.StudentId!.Value).Distinct().ToArray();
            var historicalProfiles = await AsyncExecuter.ToListAsync(profiles.Where(x => Enumerable.Contains(historicalStudentIds, x.Id)));
            foreach (var row in historicalRows)
            {
                var profile = historicalProfiles.FirstOrDefault(x => x.Id == row.StudentId);
                row.GrNumber = profile?.GrNumber ?? "";
                row.ClassDivision = profile?.ClassDivision ?? "";
            }
        }
        foreach (var baseQuery in queries)
        {
            var query = WithWorkspaceStudents(baseQuery, profiles);
            if (source != null) query = query.Where(x => x.Source.ToLower() == source);
            var categoryOptions = await AsyncExecuter.ToListAsync(query.Where(x => x.CategoryId != null)
                .Select(x => new { Id = x.CategoryId!.Value, Label = x.CategoryName }).Distinct());
            options.AddRange(categoryOptions.Select(x => new AccountsCategoryOptionDto(x.Id, x.Label)));
            query = query.Where(x => x.Date >= period.StartUtc && x.Date < period.EndUtc);
            if (input.CategoryId.HasValue) query = query.Where(x => x.CategoryId == input.CategoryId);
            query = rowFilters.Apply(query, WorkspaceFields);
            if (partyFilterIds != null) query = query.Where(x => Enumerable.Contains(partyFilterIds, x.Id));
            var search = input.Search?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(search)) query = query.Where(x => x.Reference.ToLower().Contains(search) ||
                x.StudentName.ToLower().Contains(search) || x.GrNumber.ToLower().Contains(search) ||
                x.CategoryName.ToLower().Contains(search) || x.BankLabel.ToLower().Contains(search) ||
                Enumerable.Contains(partySearchIds, x.Id));
            result.TotalCount += await AsyncExecuter.CountAsync(query);
            rows.AddRange(await AsyncExecuter.ToListAsync(OrderWorkspace(query, sortBy, input.SortDescending)
                .Take(checked(input.Page * input.PageSize))));
        }
        var historical = historicalRows.Where(x => source == null || x.Source.ToLowerInvariant() == source).ToList();
        options.AddRange(historical.Where(x => x.CategoryId.HasValue).Select(x => new AccountsCategoryOptionDto(x.CategoryId!.Value, x.CategoryName)));
        var term = input.Search?.Trim();
        historical = rowFilters.Apply(historical.AsQueryable(), WorkspaceFields).Where(x => partyFilterIds == null &&
            x.Date >= period.StartUtc && x.Date < period.EndUtc &&
            (!input.CategoryId.HasValue || x.CategoryId == input.CategoryId) &&
            (string.IsNullOrEmpty(term) || x.Reference.Contains(term, StringComparison.OrdinalIgnoreCase) ||
             x.StudentName.Contains(term, StringComparison.OrdinalIgnoreCase) || x.GrNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
             x.CategoryName.Contains(term, StringComparison.OrdinalIgnoreCase) || x.BankLabel.Contains(term, StringComparison.OrdinalIgnoreCase))).ToList();
        result.TotalCount += historical.Count;
        rows.AddRange(OrderWorkspace(historical.AsQueryable(), sortBy, input.SortDescending).Take(input.Page * input.PageSize));
        result.Categories = options.DistinctBy(x => x.Id).OrderBy(x => x.Label).ToList();
        result.Items = OrderWorkspace(rows.AsQueryable(), sortBy, input.SortDescending)
            .Skip((input.Page - 1) * input.PageSize).Take(input.PageSize).Select(x => new AccountsActivityRowDto
            {
                Id = x.Id, SourceId = x.SourceId, StudentId = x.StudentId, FinanceFeeAccountId = x.FinanceFeeAccountId, StudentName = x.StudentName.Trim(),
                GrNumber = string.IsNullOrEmpty(x.GrNumber) ? null : x.GrNumber, ClassDivision = x.ClassDivision,
                CategoryId = x.CategoryId, CategoryName = x.CategoryName, BankAccountId = x.BankAccountId,
                BankLabel = x.BankLabel, Reference = x.Reference, Source = x.Source, SourceStatus = x.Status,
                Amount = x.Amount, OccurredAt = new DateTimeOffset(DateTime.SpecifyKind(x.Date, DateTimeKind.Utc)), CanOpenSource = true
            }).ToList();
        var manualIds = result.Items.Where(x => x.Source is "Income" or "Expense").Select(x => x.Id).ToArray();
        if (manualIds.Length > 0)
        {
            var parties = await AsyncExecuter.ToListAsync(transactions.Where(x => Enumerable.Contains(manualIds, x.Id) &&
                x.AcademicYearId == input.AcademicYearId).Select(x => new { x.Id, x.ExtraProperties }));
            foreach (var row in result.Items.Where(x => x.Source is "Income" or "Expense"))
            {
                var party = parties.FirstOrDefault(x => x.Id == row.Id);
                row.Counterparty = "";
                if (party != null && party.ExtraProperties.TryGetValue("Counterparty", out var name))
                    row.Counterparty = name?.ToString() ?? "";
                if (!row.StudentId.HasValue) row.StudentName = row.Counterparty;
            }
        }
        return result;
    }

    private static IOrderedQueryable<WorkspaceRecord> OrderWorkspace(IQueryable<WorkspaceRecord> query, string column, bool descending)
    {
        IOrderedQueryable<WorkspaceRecord> ordered = column switch
        {
            "reference" => descending ? query.OrderByDescending(x => x.Reference) : query.OrderBy(x => x.Reference),
            "studentIdentity" => descending ? query.OrderByDescending(x => x.StudentId == null && x.FinanceFeeAccountId == null ? "" : x.StudentName + (x.GrNumber == "" ? "" : " (" + x.GrNumber + ")")) :
                query.OrderBy(x => x.StudentId == null && x.FinanceFeeAccountId == null ? "" : x.StudentName + (x.GrNumber == "" ? "" : " (" + x.GrNumber + ")")),
            "classDivision" => descending ? query.OrderByDescending(x => x.ClassDivision) : query.OrderBy(x => x.ClassDivision),
            "categoryName" => descending ? query.OrderByDescending(x => x.CategoryName) : query.OrderBy(x => x.CategoryName),
            "bankLabel" => descending ? query.OrderByDescending(x => x.BankLabel) : query.OrderBy(x => x.BankLabel),
            "amount" => descending ? query.OrderByDescending(x => (double)x.Amount) : query.OrderBy(x => (double)x.Amount),
            "sourceStatus" => descending ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
            _ => descending ? query.OrderByDescending(x => x.Date) : query.OrderBy(x => x.Date)
        };
        return ordered.ThenByDescending(x => x.Id);
    }

    private Task CheckWorkspacePermissionAsync(bool income) => AuthorizationService.CheckAsync(income
        ? ShikshaMitraSubmodulePermissions.Finance.Accounts_IncomeTracking.View
        : ShikshaMitraSubmodulePermissions.Finance.Accounts_ExpenseTracking.View);
}
