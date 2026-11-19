using FluentValidation;
using ShikshaMitra.Finance.Accounts.Settings.TransactionCategories.Dtos;

namespace ShikshaMitra.Finance.Accounts.Settings.TransactionCategories;

public class CreateIncomeCategoryRequestValidator : AbstractValidator<CreateIncomeCategoryRequest>
{
    public CreateIncomeCategoryRequestValidator()
    {
        RuleFor(input => input.Name).NotEmpty()
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Income Category Name is required")
            .Must(name => name?.Trim().Length <= AccountSettingConsts.MaxIncomeCategoryNameLength)
            .WithMessage("Income Category Name must be at most 40 characters");
    }
}

public class UpdateIncomeCategoryRequestValidator : AbstractValidator<UpdateIncomeCategoryRequest>
{
    public UpdateIncomeCategoryRequestValidator()
    {
        RuleFor(input => input.Name).NotEmpty()
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Income Category Name is required")
            .Must(name => name?.Trim().Length <= AccountSettingConsts.MaxIncomeCategoryNameLength)
            .WithMessage("Income Category Name must be at most 40 characters");
    }
}
