using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
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
			["studentName"] = (Expression<Func<AdjustmentListRow, string>>)(row => row.StudentName + (row.GrNumber == "" ? "" : " (" + row.GrNumber + ")")),
			["className"] = (Expression<Func<AdjustmentListRow, string>>)(row => row.StandardName + (row.DivisionName == "" ? "" : " / " + row.DivisionName)),
			["rollNumber"] = (Expression<Func<AdjustmentListRow, string>>)(row => row.RollNo),
			["requestedBy"] = (Expression<Func<AdjustmentListRow, string>>)(row => row.RequestedByName),
			["status"] = (Expression<Func<AdjustmentListRow, string>>)(row => row.StatusName),
			["adjustmentAmount"] = (Expression<Func<AdjustmentListRow, double>>)(row => row.RequestedAmount),
			["createdAt"] = (Expression<Func<AdjustmentListRow, DateTime>>)(row => row.CreatedAt)
		};

	private sealed class AdjustmentListRow
	{
		public Guid Id { get; init; }
		public Guid StudentId { get; init; }
		public Guid? CreatorId { get; init; }
		public string StudentName { get; init; } = "";
		public string GrNumber { get; init; } = "";
		public string RollNo { get; init; } = "";
		public string StandardName { get; init; } = "";
		public string DivisionName { get; init; } = "";
		public string RequestedByName { get; init; } = "";
		public string StatusName { get; init; } = "";
		public string Reason { get; init; } = "";
		public string VoucherNumber { get; init; } = "";
		public double RequestedAmount { get; init; }
		public double AppliedAmount { get; init; }
		public double RemainingAmount { get; init; }
		public FeeAdjustmentStatus Status { get; init; }
		public DateTime CreatedAt { get; init; }
	}

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
		var scopedStudents = await financeScopeAccessService.GetRequestScopedStudentIdsQueryAsync(
			input.AcademicYearId, ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.View);
		var query = (await feeAdjustmentRepository.GetQueryableAsync()).Where(request =>
			request.TenantId == CurrentTenant.Id && scopedStudents.Contains(request.StudentId) &&
			request.AcademicYearId == input.AcademicYearId && request.BoardId == input.BoardId &&
			request.EducationMediumId == input.EducationMediumId);
		if (input.StandardId.HasValue) query = query.Where(request => request.StandardId == input.StandardId.Value);
		if (input.DivisionId.HasValue) query = query.Where(request => request.DivisionId == input.DivisionId.Value);
		return query;
	}

}
