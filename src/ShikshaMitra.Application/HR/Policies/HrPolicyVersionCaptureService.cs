using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Audit;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
public class HrPolicyVersionCaptureService(
	IRepository<HrPolicyVersion, Guid> versionRepository,
	HrAuditWriter auditWriter)
	: ApplicationService, ITransientDependency
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = false
	};

	public async Task CaptureAsync(string policyType, object policyDto, string action, string? reason = null)
	{
		var normalized = HrPolicyVersionProjection.NormalizePolicyType(policyType);
		var snapshotJson = JsonSerializer.Serialize(policyDto, JsonOptions);
		var version = normalized switch
		{
			"work_calendar" => BuildWorkCalendarVersion((WorkCalendarPolicyDto) policyDto, snapshotJson, action, reason),
			"payroll" => BuildPayrollVersion((PayrollPolicyDto) policyDto, snapshotJson, action, reason),
			"attendance" => BuildAttendanceVersion((AttendancePolicyDto) policyDto, snapshotJson, action, reason),
			_ => throw new BusinessException(HRErrorCodes.HrPolicyInvalidType)
		};

		var query = await versionRepository.GetQueryableAsync();
		var exists = await AsyncExecuter.AnyAsync(query.Where(x =>
			x.PolicyType == version.PolicyType &&
			x.PolicyId == version.PolicyId &&
			x.VersionNumber == version.VersionNumber));
		if (exists)
		{
			return;
		}

		await versionRepository.InsertAsync(version, autoSave: true);
		await auditWriter.WriteAsync(
			version.AcademicYearId,
			"policy_version",
			version.Id,
			$"{version.Name} v{version.VersionNumber}",
			HrPolicyVersionProjection.NormalizeAuditAction(action),
			$"Captured {version.PolicyType} policy version {version.VersionNumber}.",
			reason,
			context: new
			{
				version.PolicyType,
				version.PolicyId,
				version.VersionNumber,
				version.Status,
				version.EffectiveFrom,
				version.EffectiveTo
			});
	}

	private HrPolicyVersion BuildWorkCalendarVersion(WorkCalendarPolicyDto dto, string snapshotJson, string action, string? reason) =>
		new(GuidGenerator.Create(), CurrentTenant.Id, "work_calendar", dto.Id, dto.AcademicYearId, dto.Version, dto.Name, dto.Status, dto.EffectiveFrom, dto.EffectiveTo, snapshotJson, action, reason);

	private HrPolicyVersion BuildPayrollVersion(PayrollPolicyDto dto, string snapshotJson, string action, string? reason) =>
		new(GuidGenerator.Create(), CurrentTenant.Id, "payroll", dto.Id, dto.AcademicYearId, dto.Version, dto.Name, dto.Status, null, null, snapshotJson, action, reason);

	private HrPolicyVersion BuildAttendanceVersion(AttendancePolicyDto dto, string snapshotJson, string action, string? reason) =>
		new(GuidGenerator.Create(), CurrentTenant.Id, "attendance", dto.Id, dto.AcademicYearId, dto.Version, dto.Name, dto.Status, null, null, snapshotJson, action, reason);
}
