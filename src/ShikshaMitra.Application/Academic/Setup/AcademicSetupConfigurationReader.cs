using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.Timetable.Settings.LectureSlots;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Setup;

[RemoteService(false)]
public class AcademicSetupConfigurationReader(
	IRepository<Subject, Guid> subjects, IRepository<ElectiveGroup, Guid> electives,
	IRepository<WeeklyLectureSlot, Guid> weekly, IRepository<LectureSlotConfig, Guid> slots,
	IRepository<HomeworkType, Guid> homework, IRepository<AssignmentType, Guid> assignments,
	IRepository<AssessmentRubric, Guid> rubrics, IRepository<GradingScale, Guid> grades,
	IRepository<ExamType, Guid> exams) : ApplicationService
{
	public async Task ReadAsync(List<AcademicSetupSettingStateDto> settings, AcademicSetupAccess.Context context)
	{
		foreach (var setting in settings)
		{
			if (!setting.CanView) continue;
			if (!context.MediumId.HasValue || setting.Key is "weekly-lectures" or "lecture-slots" && !context.StandardId.HasValue)
			{
				setting.ConfigurationState = "context-required";
				continue;
			}
			var mediumId = context.MediumId.Value;
			var standardId = context.StandardId;
			var tenantId = CurrentTenant.Id;
			var configured = setting.Key switch {
				"subjects" => await subjects.AnyAsync(x => x.TenantId == tenantId && x.EducationMediumId == mediumId && x.Status == SubjectMasterStatus.Active),
				"elective-groups" => await electives.AnyAsync(x => x.TenantId == tenantId && x.AcademicMediumId == mediumId && x.Status == SubjectMasterStatus.Active),
				"weekly-lectures" => await weekly.AnyAsync(x => x.TenantId == tenantId && x.StandardId == standardId),
				"lecture-slots" => await slots.AnyAsync(x => x.TenantId == tenantId && x.StandardId == standardId),
				"homework-types" => await homework.AnyAsync(x => x.TenantId == tenantId && x.AcademicMediumId == mediumId && x.Status == TaskTypeStatus.Active),
				"assignment-types" => await assignments.AnyAsync(x => x.TenantId == tenantId && x.AcademicMediumId == mediumId && x.Status == TaskTypeStatus.Active),
				"rubrics" => await rubrics.AnyAsync(x => x.TenantId == tenantId && x.AcademicMediumId == mediumId && x.IsTemplate &&
					(x.StandardId == null || x.StandardId == standardId) && x.Status != RubricStatus.Archived && x.Status != RubricStatus.Superseded &&
					(x.Visibility == RubricVisibility.Institution || x.CreatorId == CurrentUser.Id)),
				"grading-scales" => await grades.AnyAsync(x => x.TenantId == tenantId && x.AcademicMediumId == mediumId && (x.Status == SettingStatus.Active || x.Status == SettingStatus.SystemDefault)),
				"exam-types" => await exams.AnyAsync(x => x.TenantId == tenantId && x.AcademicMediumId == mediumId && (x.Status == SettingStatus.Active || x.Status == SettingStatus.SystemDefault)),
				_ => false
			};
			setting.ConfigurationState = configured ? "configured" : "empty";
		}
	}
}
