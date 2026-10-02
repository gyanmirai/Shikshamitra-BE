using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Attendance;

[RemoteService(false)]
public class AttendanceSettingsAppService(IRepository<StudentAttendanceConfiguration, Guid> configurations,
	IRepository<StudentAttendancePolicyVersion, Guid> versions, AttendancePolicyResolver resolver, AttendanceSettingsAccessService access)
	: ApplicationService, IAttendanceSettingsAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_SettingLeaveType.View)]
	public async Task<AttendanceSettingsDto> GetAsync(Guid? academicYearId)
	{
		await access.EnsureAsync(academicYearId, false);
		return await ReadAsync(academicYearId);
	}
	private async Task<AttendanceSettingsDto> ReadAsync(Guid? year)
	{
		var config = await configurations.FindAsync(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == year);
		var baseline = await resolver.ResolveAsync(year, Clock.Now.Date);
		var schoolDate = TimeZoneInfo.ConvertTimeFromUtc(Clock.Now.ToUniversalTime(), TimeZoneInfo.FindSystemTimeZoneById(baseline.Options.SchoolTimeZone)).Date;
		var effective = await resolver.ResolveAsync(year, schoolDate);
		var query = await versions.GetQueryableAsync();
		var history = await AsyncExecuter.ToListAsync(query.Where(x => x.TenantId == CurrentTenant.Id && (x.AcademicYearId == year || x.AcademicYearId == null))
			.OrderByDescending(x => x.CreationTime).Take(100));
		return new()
		{
			Id = config?.Id, AcademicYearId = year, ConcurrencyStamp = config?.ConcurrencyStamp,
			DraftPolicy = AttendancePolicy.FromJson(config?.DraftPolicyJson ?? effective.Policy.ToJson()), EffectivePolicy = effective.Policy,
			PublishedVersionId = effective.VersionId, PublishedVersionNumber = effective.VersionNumber, IsStarter = effective.IsStarter,
			EffectiveScopeAcademicYearId = effective.ScopeAcademicYearId,
			Versions = history.Select(x => new AttendancePolicyVersionDto { Id = x.Id, AcademicYearId = x.AcademicYearId,
				VersionNumber = x.VersionNumber, EffectiveFrom = x.EffectiveFrom, PublishedAt = x.CreationTime }).ToList()
		};
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_SettingLeaveType.Add)]
	[UnitOfWork(true)]
	public async Task<AttendanceSettingsDto> SaveDraftAsync(SaveAttendanceSettingsDto input)
	{
		await access.EnsureAsync(input.AcademicYearId, true); await access.ValidateAsync(input.Policy, input.AcademicYearId);
		var config = await configurations.FindAsync(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == input.AcademicYearId);
		EnsureStamp(config, input.ConcurrencyStamp);
		if (config == null) await configurations.InsertAsync(new(GuidGenerator.Create(), CurrentTenant.Id!.Value, input.AcademicYearId, input.Policy.ToJson()), autoSave: true);
		else { config.SaveDraft(input.Policy.ToJson()); await configurations.UpdateAsync(config, autoSave: true); }
		return await ReadAsync(input.AcademicYearId);
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_SettingLeaveType.Add)]
	public async Task<AttendanceSettingsPreviewDto> PreviewAsync(SaveAttendanceSettingsDto input)
	{
		await access.EnsureAsync(input.AcademicYearId, true); await access.ValidateAsync(input.Policy, input.AcademicYearId);
		var p = input.Policy.Defaults;
		return new()
		{
			Policy = input.Policy,
			Summary = ["Academic Calendar owns holidays, emergency closures and special working days.",
				$"{input.Policy.Exceptions.Count} scoped exceptions; division, class, stage, medium and board precedence is deterministic.",
				"One daily record per enrollment. Unmarked days remain unknown; submitted records retain their policy snapshot."],
			Examples = [new() { Label = "Late student attends", Outcome = $"Present credit: {p.LateCredit:0.##}." },
				new() { Label = "Half-day attendance", Outcome = $"Present credit: {p.HalfDayCredit:0.##}." },
				new() { Label = "Approved leave", Outcome = p.ApprovedLeaveTreatment == "Exclude" ? "Excluded from the attendance percentage denominator." : "Counts as an absence." },
				new() { Label = "Approved half-day leave", Outcome = p.ApprovedLeaveTreatment == "Exclude" && p.HalfDayLeaveTreatment == "HalfDenominator"
					? "Remaining half-day counts as 0.5 of an expected day. Physical attendance is still required; leave never means Present."
					: "The normal daily denominator remains; absence is not automatically excused." },
				new() { Label = "Shortened approved individual schedule completed", Outcome = "Full Present. The shorter expected schedule does not imply Half-day." },
				new() { Label = "17 Present, 1 Absent, 2 Unmarked", Outcome = "94.44% recorded attendance, provisional; 18 of 20 days complete." }]
		};
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_SettingLeaveType.Add)]
	[UnitOfWork(true)]
	public async Task<AttendanceSettingsDto> PublishAsync(PublishAttendanceSettingsDto input)
	{
		await access.EnsureAsync(input.AcademicYearId, true);
		var config = await configurations.FindAsync(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == input.AcademicYearId)
			?? throw new BusinessException(AttendancePolicyErrorCodes.DraftRequired);
		EnsureStamp(config, input.ConcurrencyStamp);
		var policy = AttendancePolicy.FromJson(config.DraftPolicyJson); await access.ValidateAsync(policy, input.AcademicYearId);
		await access.EnsureEffectiveDateAsync(input.AcademicYearId, input.EffectiveFrom);
		var today = TimeZoneInfo.ConvertTimeFromUtc(Clock.Now.ToUniversalTime(), TimeZoneInfo.FindSystemTimeZoneById(policy.Defaults.SchoolTimeZone)).Date;
		if (input.EffectiveFrom.Date < today) throw new BusinessException(AttendancePolicyErrorCodes.InvalidPolicy).WithData("Reason", "Publication cannot rewrite historical days");
		await versions.InsertAsync(new(GuidGenerator.Create(), CurrentTenant.Id!.Value, config.Id, input.AcademicYearId,
			config.PublishedVersionNumber + 1, input.EffectiveFrom, config.DraftPolicyJson));
		config.Publish(); await configurations.UpdateAsync(config, autoSave: true);
		return await ReadAsync(input.AcademicYearId);
	}
	private static void EnsureStamp(StudentAttendanceConfiguration? config, string? stamp)
	{
		if ((config == null && stamp != null) || (config != null && (string.IsNullOrWhiteSpace(stamp) || config.ConcurrencyStamp != stamp)))
			throw new BusinessException(AttendancePolicyErrorCodes.ConfigurationChanged);
	}
}
