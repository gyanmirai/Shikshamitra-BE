using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Finance.Reports;

public class FinanceReportRequestGuard : ITransientDependency
{
    public const string InvalidRequest = "FinanceReports:InvalidRequest";
    public const string UnsupportedReportType = "FinanceReports:UnsupportedReportType";
    public const string AccessDenied = "FinanceReports:ScopeAccessDenied";
    public const string ExportNotReady = "FinanceReports:ExportNotReady";
    public const string ExportExpired = "FinanceReports:ExportExpired";
    public const string ExportNotFound = "FinanceReports:ExportNotFound";

    private static readonly HashSet<string> SupportedReportTypes = new(StringComparer.Ordinal)
    {
        FinanceReportTypes.FeeCollectionBatchAndMonthwise,
        FinanceReportTypes.FeeCollectionBatchwise,
        FinanceReportTypes.FeeCollectionMonthAndFeeHeadwise,
        FinanceReportTypes.FeeCollectionStudentWithFeewise,
        FinanceReportTypes.FeeListChequeClearDateWise,
        FinanceReportTypes.FeeMonthlyRegister,
        FinanceReportTypes.BankDepositReport,
        FinanceReportTypes.ClasswiseFeeSetup,
        FinanceReportTypes.CustomGroupReceiptList,
        FinanceReportTypes.FeeRegisterAllStudent
    };

    public void ValidateAndNormalize(FinanceReportRequestDto request)
    {
        if (request == null)
        {
            throw new BusinessException(InvalidRequest).WithData("Field", "request");
        }

        request.ReportType = request.ReportType?.Trim() ?? string.Empty;
        if (!SupportedReportTypes.Contains(request.ReportType))
        {
            throw new BusinessException(UnsupportedReportType)
                .WithData("ReportType", request.ReportType);
        }

        request.Format = request.Format?.Trim().ToLowerInvariant() ?? string.Empty;
        if (request.Format is not ("pdf" or "excel"))
        {
            throw Invalid("Format", "Only pdf and excel are supported.");
        }

        if (request.Page < 1)
        {
            throw Invalid("Page", "Page must be at least 1.");
        }

        if (request.PageSize is < 1 or > 500)
        {
            throw Invalid("PageSize", "Page size must be between 1 and 500.");
        }

        request.Filters ??= new FinanceReportFilterDto();
        var filters = request.Filters;

        if (filters.FromDate.HasValue && filters.ToDate.HasValue && filters.FromDate > filters.ToDate)
        {
            throw Invalid("DateRange", "From date cannot be later than to date.");
        }

        filters.Months = NormalizeMonths(filters.Months);
        filters.AcademicYearIds = NormalizeIds(filters.AcademicYearIds, "AcademicYearIds");
        filters.ReceiptBookIds = NormalizeIds(filters.ReceiptBookIds, "ReceiptBookIds");
        filters.FeeHeadIds = NormalizeIds(filters.FeeHeadIds, "FeeHeadIds");
        filters.OrganizationIds = NormalizeIds(filters.OrganizationIds, "OrganizationIds");
        filters.ClassIds = NormalizeIds(filters.ClassIds, "ClassIds");
        filters.DivisionIds = NormalizeIds(filters.DivisionIds, "DivisionIds");

        ValidateOptionalId(filters.BatchId, "BatchId");
        ValidateOptionalId(filters.BoardId, "BoardId");
        ValidateOptionalId(filters.MediumId, "MediumId");
    }

    public static bool RequiresAcademicYearWideScope(string reportType) =>
        reportType is FinanceReportTypes.FeeCollectionBatchAndMonthwise
            or FinanceReportTypes.FeeCollectionBatchwise
            or FinanceReportTypes.FeeCollectionMonthAndFeeHeadwise;

    private static List<int>? NormalizeMonths(IEnumerable<int>? months)
    {
        if (months == null)
        {
            return null;
        }

        var normalized = months.Distinct().OrderBy(month => month).ToList();
        if (normalized.Count > 12 || normalized.Any(month => month is < 1 or > 12))
        {
            throw Invalid("Months", "Months must contain values from 1 through 12.");
        }

        return normalized;
    }

    private static List<Guid>? NormalizeIds(IEnumerable<Guid>? ids, string field)
    {
        if (ids == null)
        {
            return null;
        }

        var normalized = ids.Distinct().ToList();
        if (normalized.Count > 500 || normalized.Any(id => id == Guid.Empty))
        {
            throw Invalid(field, "The filter contains too many values or an empty identifier.");
        }

        return normalized;
    }

    private static void ValidateOptionalId(Guid? id, string field)
    {
        if (id == Guid.Empty)
        {
            throw Invalid(field, "The identifier cannot be empty.");
        }
    }

    private static BusinessException Invalid(string field, string reason) =>
        new BusinessException(InvalidRequest)
            .WithData("Field", field)
            .WithData("Reason", reason);
}
