using FluentValidation;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class AutomatedNotificationsSettingsDtoValidator : AbstractValidator<AutomatedNotificationsSettingsDto>
{
	public AutomatedNotificationsSettingsDtoValidator()
	{
		RuleFor(x => x.EmployeeAttendanceStatusType).IsInEnum();

		RuleFor(x => x.NotificationMethods)
			.NotNull()
			.WithMessage("NotificationMethods are required.")
			.Must(x => x != null && x.Count > 0)
			.WithMessage("At least one notification method must be selected.");

		RuleForEach(x => x.NotificationMethods).IsInEnum();
	}
}
