using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Aggregates;
using Volo.Abp.Validation;

namespace ShikshaMitra.Finance.Fees.FeeAdjustment;

public partial class FeeAdjustmentAppService
{
	private static readonly IReadOnlyDictionary<string, FinanceTableColumn> ListFilterColumns =
		new Dictionary<string, FinanceTableColumn>
		{
			["studentName"] = new(FinanceTableValueType.Text), ["className"] = new(FinanceTableValueType.Text),
			["rollNumber"] = new(FinanceTableValueType.Text), ["requestedBy"] = new(FinanceTableValueType.Text),
			["status"] = new(FinanceTableValueType.Text), ["adjustmentAmount"] = new(FinanceTableValueType.Number),
			["createdAt"] = new(FinanceTableValueType.Date)
		};
	private static readonly IReadOnlyDictionary<string, LambdaExpression> ListFilterFields =
		new Dictionary<string, LambdaExpression>
		{
			["studentName"] = (Expression<Func<FeeAdjustmentApplicationItemDto, string>>)(row => StudentDisplay(row)),
			["className"] = (Expression<Func<FeeAdjustmentApplicationItemDto, string>>)(row => ClassDisplay(row)),
			["rollNumber"] = (Expression<Func<FeeAdjustmentApplicationItemDto, string>>)(row => row.RollNo),
			["requestedBy"] = (Expression<Func<FeeAdjustmentApplicationItemDto, string?>>)(row => row.RequestedByName),
			["status"] = (Expression<Func<FeeAdjustmentApplicationItemDto, string>>)(row => StatusDisplay(row)),
			["adjustmentAmount"] = (Expression<Func<FeeAdjustmentApplicationItemDto, double>>)(row => row.RequestedAmount),
			["createdAt"] = (Expression<Func<FeeAdjustmentApplicationItemDto, DateTime>>)(row => row.CreatedAt)
		};

	private static FinanceTableQuery ParseListFilters(string? filters, string? search)
	{
		try { return FinanceTableQuery.Parse(filters, search, null, null, ListFilterColumns); }
		catch (ArgumentNullException ex) when (ex.ParamName == "key")
		{
			throw new AbpValidationException("Invalid adjustment table filters.");
		}
	}

	private async Task<IQueryable<FeeAdjustmentRequest>> BuildApplicationsListQueryAsync(GetFeeAdjustmentApplicationsInput input)
	{
		var scopedStudents = await financeScopeAccessService.GetScopedStudentIdsQueryAsync();
		var query = (await feeAdjustmentRepository.GetQueryableAsync()).Where(request =>
			request.TenantId == CurrentTenant.Id && scopedStudents.Contains(request.StudentId) &&
			request.AcademicYearId == input.AcademicYearId && request.BoardId == input.BoardId &&
			request.EducationMediumId == input.EducationMediumId);
		if (input.StandardId.HasValue) query = query.Where(request => request.StandardId == input.StandardId.Value);
		if (input.DivisionId.HasValue) query = query.Where(request => request.DivisionId == input.DivisionId.Value);
		return query;
	}

	private static string StudentDisplay(FeeAdjustmentApplicationItemDto row) =>
		(string.IsNullOrEmpty(row.StudentName) ? "-" : row.StudentName) +
		(string.IsNullOrEmpty(row.GrNumber) ? "" : " (" + row.GrNumber + ")");
	private static string ClassDisplay(FeeAdjustmentApplicationItemDto row) =>
		!string.IsNullOrEmpty(row.StandardName) && !string.IsNullOrEmpty(row.DivisionName)
			? row.StandardName + " / " + row.DivisionName : string.IsNullOrEmpty(row.StandardName) ? "-" : row.StandardName;
	// Match the existing table mapper's UI states; wire enum identities are unchanged.
	private static string StatusDisplay(FeeAdjustmentApplicationItemDto row) => row.Status switch
	{
		FeeAdjustmentStatus.Pending => "Pending", FeeAdjustmentStatus.Rejected => "Rejected", _ => "Approved"
	};
	private static bool MatchesListSearch(FeeAdjustmentApplicationItemDto row, FeeAdjustmentRequest request, string search) =>
		string.Join(" ", StudentDisplay(row), ClassDisplay(row), row.RollNo, row.RequestedByName, StatusDisplay(row),
			row.RequestedAmount.ToString(CultureInfo.InvariantCulture), row.CreatedAt.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
			row.CreatedAt.ToString("O", CultureInfo.InvariantCulture), request.VoucherNumber, request.Reason)
		.Contains(search, StringComparison.OrdinalIgnoreCase);
}
