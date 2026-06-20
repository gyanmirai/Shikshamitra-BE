using Microsoft.AspNetCore.Identity;
using ShikshaMitra.Common;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Calendar.HolidayTypes;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Settings;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.ProfessionalManagement;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private async Task<AttendancePolicy?> EnsureAttendancePolicyAsync(
		AcademicYear academicYear,
		WorkCalendarPolicy workCalendar,
		string name,
		bool isDefault,
		double fullDayThresholdPercentage,
		double halfDayThresholdPercentage,
		double absentThresholdPercentage,
		AttendanceMissingPunchBehavior missingPunchBehavior,
		bool manualCorrectionAllowed,
		int graceTime,
		int maxLateAllowedPerMonth,
		AttendancePenaltyType penaltyType,
		int penaltyCountThreshold,
		bool earlyExitEnabled,
		int earlyExitGraceTime,
		bool overtimeEnabled,
		AttendanceOvertimeRate? overtimeRate,
		int? minOvertimeMinutes,
		AttendanceHolidayWorkBehavior holidayWorkBehavior,
		bool payrollPreviewEnabled,
		decimal? penaltyDeductionAmount = null)
	{
		var existing = await _attendancePolicyRepository.FirstOrDefaultAsync(x =>
			x.AcademicYearId == academicYear.Id &&
			x.Name == name);
		if (existing != null)
		{
			if (isDefault && !await _attendancePolicyRepository.AnyAsync(x =>
				x.AcademicYearId == academicYear.Id &&
				x.IsDefault &&
				x.Status == HrPolicyStatus.Active))
			{
				if (existing.Status == HrPolicyStatus.Active)
				{
					existing.SetDefault(true);
				}
				else
				{
					existing.Update(
						workCalendar.Id,
						name,
						HrPolicyStatus.Active,
						isDefault: true,
						existing.CaptureMode,
						fullDayThresholdPercentage,
						halfDayThresholdPercentage,
						absentThresholdPercentage,
						missingPunchBehavior,
						manualCorrectionAllowed,
						graceTime,
						maxLateAllowedPerMonth,
						penaltyType,
						penaltyCountThreshold,
						earlyExitEnabled,
						earlyExitGraceTime,
						overtimeEnabled,
						overtimeRate,
						minOvertimeMinutes,
						holidayWorkBehavior,
						payrollPreviewEnabled,
						penaltyDeductionAmount);
				}

				await _attendancePolicyRepository.UpdateAsync(existing, autoSave: false);
			}

			return existing;
		}

		if (isDefault && await _attendancePolicyRepository.AnyAsync(x =>
			x.AcademicYearId == academicYear.Id &&
			x.IsDefault &&
			x.Status == HrPolicyStatus.Active))
		{
			isDefault = false;
		}

		var policy = new AttendancePolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYear.Id,
			workCalendar.Id,
			name,
			HrPolicyStatus.Active,
			isDefault,
			AttendanceCaptureMode.Integrated,
			fullDayThresholdPercentage,
			halfDayThresholdPercentage,
			absentThresholdPercentage,
			missingPunchBehavior,
			manualCorrectionAllowed,
			graceTime,
			maxLateAllowedPerMonth,
			penaltyType,
			penaltyCountThreshold,
			earlyExitEnabled,
			earlyExitGraceTime,
			overtimeEnabled,
			overtimeRate,
			minOvertimeMinutes,
			holidayWorkBehavior,
			payrollPreviewEnabled,
			penaltyDeductionAmount);
		await _attendancePolicyRepository.InsertAsync(policy, autoSave: false);
		return policy;
	}
}
