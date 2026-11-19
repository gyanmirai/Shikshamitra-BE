using FluentValidation;
using ShikshaMitra.Finance.Accounts.Settings.Dtos;
using ShikshaMitra.Finance.Accounts.Settings.TransactionCategories.Dtos;

namespace ShikshaMitra.Finance.Accounts.Settings;

public class AccountHeadUpsertRequestValidator : AbstractValidator<AccountHeadUpsertRequest>
{
	public AccountHeadUpsertRequestValidator()
	{
		RuleFor(x => x.HeadName).NotEmpty().MaximumLength(AccountSettingConsts.MaxNameLength);
		RuleFor(x => x.HeadType).IsInEnum();
		RuleFor(x => x.Notes).MaximumLength(AccountSettingConsts.MaxDescriptionLength);
	}
}

public class TransactionCategoryRequestValidator : AbstractValidator<CreateTransactionCategoryRequest>
{
	public TransactionCategoryRequestValidator()
	{
		RuleFor(x => x.Name).NotEmpty().MaximumLength(AccountSettingConsts.MaxNameLength);
		RuleFor(x => x.Description).MaximumLength(AccountSettingConsts.MaxDescriptionLength);
	}
}

public class UpdateTransactionCategoryRequestValidator : AbstractValidator<UpdateTransactionCategoryRequest>
{
	public UpdateTransactionCategoryRequestValidator()
	{
		RuleFor(x => x.Name).NotEmpty().MaximumLength(AccountSettingConsts.MaxNameLength);
		RuleFor(x => x.Description).MaximumLength(AccountSettingConsts.MaxDescriptionLength);
	}
}

public class AccountSettingStatusUpdateRequestValidator : AbstractValidator<AccountSettingStatusUpdateRequest>
{
	public AccountSettingStatusUpdateRequestValidator()
	{
		RuleFor(x => x.DeactivationReason).NotEmpty().When(x => !x.IsActive)
			.MaximumLength(AccountSettingConsts.MaxDeactivationReasonLength);
	}
}

public class CreateBankAccountRequestValidator : AbstractValidator<Dtos.CreateBankAccountRequest>
{
	public CreateBankAccountRequestValidator()
	{
		RuleFor(x => x.Code).MaximumLength(AccountSettingConsts.MaxCodeLength).Matches("^[A-Za-z0-9._-]+$")
			.When(x => !string.IsNullOrWhiteSpace(x.Code));
		RuleFor(x => x.BankName).NotEmpty().MaximumLength(AccountSettingConsts.MaxNameLength);
		RuleFor(x => x.BranchName).NotEmpty().MaximumLength(AccountSettingConsts.MaxNameLength);
		RuleFor(x => x.AccountHolderName).NotEmpty().MaximumLength(AccountSettingConsts.MaxNameLength);
		RuleFor(x => x.AccountType).IsInEnum();
		RuleFor(x => x.AccountNumber).Matches("^[0-9]{6,18}$");
		RuleFor(x => x.IfscCode).Matches("^[A-Za-z]{4}0[A-Za-z0-9]{6}$");
		RuleFor(x => x.UpiId).Matches("^[\\w.-]+@[\\w.-]+$").When(x => !string.IsNullOrWhiteSpace(x.UpiId));
		RuleFor(x => x).Must(x => !x.EffectiveFrom.HasValue || !x.EffectiveTo.HasValue || x.EffectiveTo >= x.EffectiveFrom)
			.WithMessage("Effective To must be on or after Effective From.");
	}
}

public class UpdateBankAccountRequestValidator : AbstractValidator<Dtos.UpdateBankAccountRequest>
{
	public UpdateBankAccountRequestValidator()
	{
		RuleFor(x => x.Code).MaximumLength(AccountSettingConsts.MaxCodeLength).Matches("^[A-Za-z0-9._-]+$")
			.When(x => !string.IsNullOrWhiteSpace(x.Code));
		RuleFor(x => x.BankName).NotEmpty().MaximumLength(AccountSettingConsts.MaxNameLength);
		RuleFor(x => x.BranchName).NotEmpty().MaximumLength(AccountSettingConsts.MaxNameLength);
		RuleFor(x => x.AccountHolderName).NotEmpty().MaximumLength(AccountSettingConsts.MaxNameLength);
		RuleFor(x => x.AccountType).IsInEnum();
		RuleFor(x => x.AccountNumber).Matches("^[0-9]{6,18}$");
		RuleFor(x => x.IfscCode).Matches("^[A-Za-z]{4}0[A-Za-z0-9]{6}$");
		RuleFor(x => x.UpiId).Matches("^[\\w.-]+@[\\w.-]+$").When(x => !string.IsNullOrWhiteSpace(x.UpiId));
		RuleFor(x => x).Must(x => !x.EffectiveFrom.HasValue || !x.EffectiveTo.HasValue || x.EffectiveTo >= x.EffectiveFrom)
			.WithMessage("Effective To must be on or after Effective From.");
	}
}
