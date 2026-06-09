using System.Threading.Tasks;
using ShikshaMitra.HR.Audit;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Policies;

public class AttendancePolicyHistoryWriter(
	HrPolicyVersionAppService versionAppService,
	HrAuditWriter auditWriter)
	: ITransientDependency
{
	private const string PolicyType = "attendance";
	private const string AuditEntityType = "attendance_policy";

	public async Task CreatedAsync(AttendancePolicyDto policy)
	{
		await CaptureAsync(policy, "created");
		await WriteAsync(policy, "created", $"Created attendance policy {policy.Name}.");
	}

	public async Task EditedAsync(AttendancePolicyDto before, AttendancePolicyDto policy)
	{
		await CaptureAsync(policy, "edited");
		await auditWriter.WriteAsync(
			policy.AcademicYearId,
			AuditEntityType,
			policy.Id,
			policy.Name,
			"edited",
			$"Edited attendance policy {policy.Name}.",
			changedFields: HrAuditWriter.Diff(before, policy, "lastModificationTime", "lastModifierId"));
	}

	public async Task ClonedAsync(AttendancePolicyDto policy, AttendancePolicy source)
	{
		await CaptureAsync(policy, "cloned");
		await auditWriter.WriteAsync(
			policy.AcademicYearId,
			AuditEntityType,
			policy.Id,
			policy.Name,
			"cloned",
			$"Cloned attendance policy from {source.Name}.",
			context: new { sourcePolicyId = source.Id, sourcePolicyName = source.Name });
	}

	public async Task ArchivedAsync(AttendancePolicyDto policy)
	{
		await CaptureAsync(policy, "archived");
		await WriteAsync(policy, "archived", $"Archived attendance policy {policy.Name}.");
	}

	public async Task UnarchivedAsync(AttendancePolicyDto policy)
	{
		await CaptureAsync(policy, "unarchived");
		await WriteAsync(policy, "unarchived", $"Unarchived attendance policy {policy.Name} as draft.");
	}

	public async Task SetDefaultAsync(AttendancePolicyDto policy)
	{
		await CaptureAsync(policy, "set-default");
		await WriteAsync(policy, "set_default", $"Set attendance policy {policy.Name} as default.");
	}

	private Task CaptureAsync(AttendancePolicyDto policy, string action) =>
		versionAppService.CaptureAsync(PolicyType, policy, action);

	private Task WriteAsync(AttendancePolicyDto policy, string action, string summary) =>
		auditWriter.WriteAsync(policy.AcademicYearId, AuditEntityType, policy.Id, policy.Name, action, summary);
}
