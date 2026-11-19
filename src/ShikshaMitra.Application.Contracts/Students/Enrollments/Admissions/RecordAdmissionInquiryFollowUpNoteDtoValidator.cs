using FluentValidation;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class RecordAdmissionInquiryFollowUpNoteDtoValidator : AbstractValidator<RecordAdmissionInquiryFollowUpNoteDto>
{
	public RecordAdmissionInquiryFollowUpNoteDtoValidator()
	{
		RuleFor(input => input.ConcurrencyStamp).NotEmpty().MaximumLength(40);
		RuleFor(input => input.InquiryConcurrencyStamp).NotEmpty().MaximumLength(40);
		RuleFor(input => input.Note).NotEmpty().Must(note => note != null && note.Trim().Length is > 0 and <= 500)
			.WithMessage("Enter a follow-up note of up to 500 characters.");
		RuleFor(input => input.Status).Must(AdmissionInquiryStatusRules.IsBusinessStatus)
			.WithMessage("Choose Pending, Interested, or Not interested.");
		RuleFor(input => input.ReminderAt).NotNull().When(input => input.Status != AdmissionInquiryStatus.NotInterested)
			.WithMessage("Choose the next follow-up reminder.");
	}
}
