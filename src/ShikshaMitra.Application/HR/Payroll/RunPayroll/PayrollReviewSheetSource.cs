using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using ShikshaMitra.HR.Reports;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Uow;
using Permission = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollReviewSheetSource(PayrollApprovalAccess access, PayrollSubmissionWorkflow submissions,
	PayrollSubmissionEvidence evidence, PayrollPreviewRunAppService previews, IPayrollInputRevisionStore revisions,
	IRepository<PayrollEmployeeClaim, Guid> claims, IAsyncQueryableExecuter queries, IDataProtectionProvider protection,
	IUnitOfWorkManager units)
	: ITransientDependency
{
	internal const string Table = "payroll-review-sheet";
	internal const string Title = "Payroll review sheet | Pending finalization";
	private static readonly (string Id, string Label)[] Columns =
	[
		("employeeName", "Staff name"), ("employeeIdentifier", "Staff code"), ("payableDays", "Payable days"),
		("totalEarnings", "Total earnings (INR)"), ("totalDeduction", "Total deductions (INR)"), ("netSalary", "Net pay (INR)"),
		("earningBreakdown", "Earnings breakdown (INR)"), ("deductionBreakdown", "Deduction breakdown (INR)")
	];
	internal static readonly string[] Fields = Columns.Select(x => x.Id).ToArray();
	internal static List<HrTableExportColumn> DisplayColumns() => Columns.Select(x => new HrTableExportColumn { Id = x.Id, Label = x.Label }).ToList();
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
	private readonly IDataProtector protector = protection.CreateProtector("ShikshaMitra.PayrollReviewSheet.v1");

	internal async Task<JsonElement> BindAsync(JsonElement query)
	{
		var selected = ReadSelection(query);
		var data = await ReadAsync(selected);
		return JsonSerializer.SerializeToElement(new
		{
			selected.Month, selected.AcademicYearId, selected.PreviewRunId, selected.EmployeeIds,
			selected.SubmissionId, selected.Version, proof = protector.Protect(data.Fingerprint)
		}, Json);
	}

	internal async Task<(string Month, List<RunPayrollItem> Items)> VerifyAsync(JsonElement query)
	{
		var selected = ReadSelection(query);
		if (!query.TryGetProperty("proof", out var proof) || proof.ValueKind != JsonValueKind.String)
			throw new AbpAuthorizationException();
		string expected;
		try { expected = protector.Unprotect(proof.GetString()!); }
		catch (CryptographicException) { throw new AbpAuthorizationException(); }
		var data = await ReadAsync(selected);
		if (expected != data.Fingerprint) throw new BusinessException("PAYROLL_REVIEW_SHEET_EXPIRED");
		return (data.Month, data.Items);
	}

	private async Task<(string Month, List<RunPayrollItem> Items, string Fingerprint)> ReadAsync(Selection selected)
	{
		using var unit = units.Begin(requiresNew: true, isTransactional: true);
		var revision = await revisions.LockAsync();
		var actor = access.ActorId;
		if (!await access.CanAsync(actor, Permission.View, Permission.ExportReview)) throw new AbpAuthorizationException();
		string period;
		List<PayrollVerifiedRow> verified;
		Dictionary<Guid, long> generations = [];
		if (selected.SubmissionId.HasValue)
		{
			if (selected.Version is not > 0 || selected.PreviewRunId.HasValue || selected.EmployeeIds != null)
				throw new BusinessException("PAYROLL_REVIEW_SHEET_SELECTION_INVALID");
			(period, verified) = await submissions.ReadPendingForExportAsync(selected.SubmissionId.Value, selected.Version.Value, revision);
		}
		else
		{
			var ids = selected.EmployeeIds?.Order().ToList();
			if (selected.PreviewRunId == null || ids is not { Count: > 0 and <= 500 } ||
				ids.Contains(Guid.Empty) || ids.Distinct().Count() != ids.Count || string.IsNullOrWhiteSpace(selected.Month))
				throw new BusinessException("PAYROLL_REVIEW_SHEET_SELECTION_INVALID");
			(_, _, period) = PayrollRunRules.ParseMonth(selected.Month);
			var run = await previews.GetAuthorizedRunAsync(selected.PreviewRunId.Value);
			verified = await evidence.ReadCurrentAsync(access.TenantId, run.Id, revision,
				period, selected.AcademicYearId, ids) ?? throw new BusinessException("PAYROLL_REVIEW_SHEET_EXPIRED");
			var state = await queries.ToListAsync((await claims.GetQueryableAsync()).Where(x => x.Month == period && ids.Contains(x.EmployeeId)));
			if (state.Any(x => x.SubmissionId.HasValue)) throw new BusinessException("PAYROLL_REVIEW_SHEET_EXPIRED");
			generations = state.ToDictionary(x => x.EmployeeId, x => x.Generation);
		}
		var fingerprint = PayrollSubmissionEvidence.Fingerprint(new
		{
			Tenant = access.TenantId, Actor = actor, period, selected.SubmissionId, selected.Version,
			Rows = verified.Select(x => new { x.Digest, Generation = generations.GetValueOrDefault(x.Preview.EmployeeId) })
		});
		await unit.CompleteAsync();
		return (period, verified.Select(x => x.Item).OrderBy(x => x.EmployeeName).ThenBy(x => x.EmployeeId).ToList(), fingerprint);
	}

	private static Selection ReadSelection(JsonElement query)
	{
		try { return query.Deserialize<Selection>(Json) ?? throw new BusinessException("PAYROLL_REVIEW_SHEET_SELECTION_INVALID"); }
		catch (JsonException) { throw new BusinessException("PAYROLL_REVIEW_SHEET_SELECTION_INVALID"); }
	}

	private sealed record Selection(string? Month, Guid? AcademicYearId, Guid? PreviewRunId, List<Guid>? EmployeeIds,
		Guid? SubmissionId, int? Version);
}
