using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.Attendance;

public static class AttendancePolicyErrorCodes
{
	public const string InvalidPolicy = "Students:Attendance:020001";
	public const string AmbiguousRules = "Students:Attendance:020002";
	public const string ConfigurationChanged = "Students:Attendance:020003";
	public const string DraftRequired = "Students:Attendance:020004";
	public const string InvalidSchedule = "Students:Attendance:020005";
	public const string ScheduleOverlap = "Students:Attendance:020006";
	public const string TenantRequired = "Students:Attendance:020007";
}

public sealed class AttendancePolicyOptions
{
	public bool AllowLate { get; set; } = true;
	public bool AllowHalfDay { get; set; } = true;
	public decimal LateCredit { get; set; } = 1m;
	public decimal HalfDayCredit { get; set; } = .5m;
	public string ApprovedLeaveTreatment { get; set; } = "Exclude";
	public string HalfDayLeaveTreatment { get; set; } = "HalfDenominator";
	public bool SameDayCorrectionRequiresReason { get; set; } = true;
	public bool PriorDayCorrectionRequiresApproval { get; set; } = true;
	public bool LockedCorrectionRequiresApproval { get; set; } = true;
	public decimal LowAttendanceThreshold { get; set; } = 75m;
	public string MarkingCutoff { get; set; } = "14:00";
	public string SchoolTimeZone { get; set; } = "Asia/Kolkata";
}

public sealed record AttendancePolicyScope(Guid? BoardId = null, Guid? MediumId = null, Guid? StageId = null,
	Guid? StandardId = null, Guid? DivisionId = null);

public sealed class AttendancePolicyException
{
	public Guid Id { get; set; }
	public string Name { get; set; } = "";
	public Guid? BoardId { get; set; }
	public Guid? MediumId { get; set; }
	public Guid? StageId { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? DivisionId { get; set; }
	public AttendancePolicyOptions Options { get; set; } = new();
	public int Specificity => DivisionId.HasValue ? 5 : StandardId.HasValue ? 4 : StageId.HasValue ? 3 : MediumId.HasValue ? 2 : BoardId.HasValue ? 1 : 0;
	public bool Matches(AttendancePolicyScope scope) => (!BoardId.HasValue || BoardId == scope.BoardId)
		&& (!MediumId.HasValue || MediumId == scope.MediumId) && (!StageId.HasValue || StageId == scope.StageId)
		&& (!StandardId.HasValue || StandardId == scope.StandardId) && (!DivisionId.HasValue || DivisionId == scope.DivisionId);
}

public sealed class AttendancePolicy
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	public int SchemaVersion { get; set; } = 1;
	public AttendancePolicyOptions Defaults { get; set; } = new();
	public List<AttendancePolicyException> Exceptions { get; set; } = [];
	public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
	public static AttendancePolicy FromJson(string json)
	{
		try { return JsonSerializer.Deserialize<AttendancePolicy>(json, JsonOptions) ?? throw new BusinessException(AttendancePolicyErrorCodes.InvalidPolicy); }
		catch (JsonException) { throw new BusinessException(AttendancePolicyErrorCodes.InvalidPolicy); }
	}
	public AttendancePolicyOptions Resolve(AttendancePolicyScope? scope) => scope == null ? Defaults :
		Exceptions.Where(x => x.Matches(scope)).OrderByDescending(x => x.Specificity).Select(x => x.Options).FirstOrDefault() ?? Defaults;
	public static void Validate(AttendancePolicy policy)
	{
		if (policy == null || policy.SchemaVersion != 1 || policy.Defaults == null || policy.Exceptions == null ||
			policy.Exceptions.Count > 200 || policy.Exceptions.Any(x => x == null))
			throw new BusinessException(AttendancePolicyErrorCodes.InvalidPolicy);
		ValidateOptions(policy.Defaults);
		if (policy.Exceptions.Select(x => x.Id).Distinct().Count() != policy.Exceptions.Count) throw new BusinessException(AttendancePolicyErrorCodes.InvalidPolicy);
		foreach (var rule in policy.Exceptions)
		{
			if (rule.Id == Guid.Empty || string.IsNullOrWhiteSpace(rule.Name) || rule.Name.Length > 150 || rule.Specificity == 0 ||
				new[] { rule.BoardId, rule.MediumId, rule.StageId, rule.StandardId, rule.DivisionId }.Any(x => x == Guid.Empty))
				throw new BusinessException(AttendancePolicyErrorCodes.InvalidPolicy);
			ValidateOptions(rule.Options);
		}
		for (var i = 0; i < policy.Exceptions.Count; i++)
			for (var j = i + 1; j < policy.Exceptions.Count; j++)
			{
				var a = policy.Exceptions[i]; var b = policy.Exceptions[j];
				if (a.Specificity == b.Specificity && Compatible(a.BoardId, b.BoardId) && Compatible(a.MediumId, b.MediumId)
					&& Compatible(a.StageId, b.StageId) && Compatible(a.StandardId, b.StandardId) && Compatible(a.DivisionId, b.DivisionId))
					throw new BusinessException(AttendancePolicyErrorCodes.AmbiguousRules).WithData("Rules", $"{a.Name}, {b.Name}");
			}
	}
	private static bool Compatible(Guid? a, Guid? b) => !a.HasValue || !b.HasValue || a == b;
	private static void ValidateOptions(AttendancePolicyOptions options)
	{
		if (options == null || options.LateCredit < 0 || options.LateCredit > 1 || options.HalfDayCredit < 0 || options.HalfDayCredit > 1 ||
			options.ApprovedLeaveTreatment is not ("Exclude" or "Absent") || options.HalfDayLeaveTreatment is not ("Absent" or "HalfDenominator") ||
			options.LowAttendanceThreshold < 0 || options.LowAttendanceThreshold > 100 ||
			!TimeOnly.TryParseExact(options.MarkingCutoff, "HH:mm", out _) || string.IsNullOrWhiteSpace(options.SchoolTimeZone))
			throw new BusinessException(AttendancePolicyErrorCodes.InvalidPolicy);
		try { TimeZoneInfo.FindSystemTimeZoneById(options.SchoolTimeZone); }
		catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { throw new BusinessException(AttendancePolicyErrorCodes.InvalidPolicy); }
	}
}
