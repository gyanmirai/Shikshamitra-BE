using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public static class LeavePolicySnapshots
{
	public static LeavePolicySnapshotDto Capture(LeavePolicy policy) => new()
	{
		Id = policy.Id, AcademicYearId = policy.AcademicYearId, Version = policy.Version,
		PolicyTitle = policy.PolicyName, Description = policy.Description,
		Status = policy.Status.ToString(), IsDefault = policy.IsDefault,
		EffectiveFrom = policy.EffectiveFrom, EffectiveTo = policy.EffectiveTo,
		Rules = policy.Rules.Where(x => x.IsActive).OrderBy(x => x.PaidType).Select(x => new LeavePolicyRuleRequest
		{
			PaidType = LeavePolicyProjection.ToPaidType(x.PaidType), PayrollCategory = x.PayrollCategory,
			EntitlementMode = LeavePolicyProjection.ToEntitlementMode(x.EntitlementMode),
			AccrualPolicyType = LeavePolicyProjection.ToAccrualPolicyType(x.AccrualPolicyType),
			LeaveCount = x.LeaveCount, CarryForward = x.CarryForward, CarryForwardLimit = x.CarryForwardLimit,
			EncashmentAllowed = x.Encashable, EncashmentTiming = LeavePolicyProjection.ToEncashmentTiming(x.EncashmentTiming),
			RequiresApproval = x.RequiresApproval, MinNoticeDays = x.MinNoticeDays, MaxDaysPerRequest = x.MaxDaysPerRequest,
			AllowHalfDay = x.AllowHalfDay, AttachmentRequiredAfterDays = x.AttachmentRequiredAfterDays,
			AllowNegativeBalance = x.AllowNegativeBalance, IsActive = true
		}).ToList()
	};

	public static string Configuration(LeavePolicyCreateRequest snapshot) => JsonSerializer.Serialize(new
	{
		snapshot.PolicyTitle, snapshot.Description, snapshot.Status, snapshot.IsDefault,
		snapshot.EffectiveFrom, snapshot.EffectiveTo,
		Rules = snapshot.Rules.OrderBy(x => x.PaidType)
	});
}
