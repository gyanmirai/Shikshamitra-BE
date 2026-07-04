using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Compliance;
using ShikshaMitra.Students.Records;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using static ShikshaMitra.Dashboard.DashboardSummaryMutator;
using static ShikshaMitra.Dashboard.DashboardTemplates;
using static ShikshaMitra.Dashboard.DashboardZones;

namespace ShikshaMitra.Dashboard;

public class DashboardComplianceReadinessSliceProvider(
	IRepository<Student, Guid> studentRepository,
	StudentScopeAccessService studentScopeAccessService,
	IRepository<StudentIdentityCompliance, Guid> studentIdentityComplianceRepository,
	IAsyncQueryableExecuter asyncExecuter,
	IPermissionChecker permissionChecker)
	: IDashboardSummarySliceProvider, ITransientDependency
{
	public async Task ApplyAsync(DashboardSummarySliceContext context)
	{
		var summary = context.Summary;
		if (summary.DashboardTemplate != Leadership)
		{
			return;
		}

		if (!await HasAnyPermissionAsync([ShikshaMitraSubmodulePermissions.Students.IdentityCompliance.View]))
		{
			return;
		}

		try
		{
			var scopedStudentIdsQuery = await GetScopedStudentIdsQueryAsync();
			var totalStudents = await asyncExecuter.CountAsync(scopedStudentIdsQuery);
			if (totalStudents == 0)
			{
				UpdateWidget(
					summary.KpiCards,
					"leadership-compliance",
					null,
					"Open",
					"No scoped students available for APAAR, PEN, or UDISE readiness",
					"info");
				return;
			}

			var complianceQuery = await studentIdentityComplianceRepository.GetQueryableAsync();
			var rows = await asyncExecuter.ToListAsync(complianceQuery
				.Where(c => scopedStudentIdsQuery.Contains(c.StudentId))
				.Select(c => new StudentComplianceReadinessRow(
					c.StudentId,
					c.ApaarId,
					c.ApaarConsentStatus,
					c.ApaarVerificationStatus,
					c.PenNumber,
					c.UdiseStudentCode,
					c.UdiseSyncStatus)));

			var apaarReady = rows.Count(IsApaarReady);
			var penReady = rows.Count(r => !string.IsNullOrWhiteSpace(r.PenNumber));
			var udiseReady = rows.Count(IsUdiseReady);
			var readyPoints = apaarReady + penReady + udiseReady;
			var totalPoints = totalStudents * 3;
			var readiness = Math.Round((decimal)readyPoints / totalPoints * 100m, 1);
			var gaps = totalPoints - readyPoints;

			UpdateWidget(
				summary.KpiCards,
				"leadership-compliance",
				null,
				$"{readiness:0.#}%",
				$"{apaarReady}/{totalStudents} APAAR, {penReady}/{totalStudents} PEN, {udiseReady}/{totalStudents} UDISE local readiness",
				readiness >= 90 ? "success" : readiness >= 70 ? "warning" : "error",
				health: gaps == 0 ? "good" : "attention");
		}
		catch
		{
			SetZoneError(summary, Kpis, "Compliance readiness could not be loaded.");
		}
	}

	private async Task<IQueryable<Guid>> GetScopedStudentIdsQueryAsync()
	{
		var students = await studentRepository.GetQueryableAsync();
		var scopedStudents = await studentScopeAccessService.ApplyCurrentUserScopeAsync(students);
		return scopedStudents.Select(s => s.Id);
	}

	private async Task<bool> HasAnyPermissionAsync(string[] permissionNames)
	{
		foreach (var permissionName in permissionNames)
		{
			if (await permissionChecker.IsGrantedAsync(permissionName))
			{
				return true;
			}
		}

		return false;
	}

	private static bool IsApaarReady(StudentComplianceReadinessRow row) =>
		!string.IsNullOrWhiteSpace(row.ApaarId) &&
		row.ApaarConsentStatus == ConsentStatus.Granted &&
		row.ApaarVerificationStatus is IdentityVerificationStatus.Pending or IdentityVerificationStatus.Verified;

	private static bool IsUdiseReady(StudentComplianceReadinessRow row) =>
		!string.IsNullOrWhiteSpace(row.UdiseStudentCode) &&
		row.UdiseSyncStatus is UdiseSyncStatus.Ready or UdiseSyncStatus.Exported;

	private sealed record StudentComplianceReadinessRow(
		Guid StudentId,
		string? ApaarId,
		ConsentStatus ApaarConsentStatus,
		IdentityVerificationStatus ApaarVerificationStatus,
		string? PenNumber,
		string? UdiseStudentCode,
		UdiseSyncStatus UdiseSyncStatus);
}
