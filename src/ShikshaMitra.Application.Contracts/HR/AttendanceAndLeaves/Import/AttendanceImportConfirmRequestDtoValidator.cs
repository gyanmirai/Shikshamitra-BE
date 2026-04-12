using FluentValidation;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Import;

public class AttendanceImportConfirmRequestDtoValidator : AbstractValidator<AttendanceImportConfirmRequestDto>
{
	public AttendanceImportConfirmRequestDtoValidator()
	{
		RuleFor(x => x.PreviewToken).NotEmpty();
	}
}
