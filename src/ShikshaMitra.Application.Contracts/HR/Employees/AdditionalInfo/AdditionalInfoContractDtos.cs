using FluentValidation;

namespace ShikshaMitra.HR.Employees.AdditionalInfo;

public class AdditionalInfoContract
{
	public string? BplCardNo { get; set; }

	public bool PersonWithDisability { get; set; }

	public string? SecondLanguage { get; set; }

	public bool IsLibraryMember { get; set; }

	public bool IsTransportMember { get; set; }
}

public class AdditionalInfoContractValidator : AbstractValidator<AdditionalInfoContract>
{
	public AdditionalInfoContractValidator()
	{
		RuleFor(x => x.BplCardNo).MaximumLength(50);
		RuleFor(x => x.SecondLanguage).MaximumLength(50);
	}
}
