namespace ShikshaMitra.Finance.Accounts.Settings;

public static class AccountSettingErrorCodes
{
	public const string SystemCategoryReadOnly = "Finance:Accounts:Settings:SystemCategoryReadOnly";
	public const string DuplicateCode = "Finance:Accounts:Settings:DuplicateCode";
	public const string DuplicateName = "Finance:Accounts:Settings:DuplicateName";
	public const string InvalidAccountHeadType = "Finance:Accounts:Settings:InvalidAccountHeadType";
	public const string ParentAccountHeadInvalid = "Finance:Accounts:Settings:ParentAccountHeadInvalid";
	public const string ParentAccountHeadCycle = "Finance:Accounts:Settings:ParentAccountHeadCycle";
	public const string AccountHeadInUse = "Finance:Accounts:Settings:AccountHeadInUse";
	public const string CategoryInUse = "Finance:Accounts:Settings:CategoryInUse";
	public const string BankAccountInUse = "Finance:Accounts:Settings:BankAccountInUse";
	public const string ActiveRecordCannotBeDeleted = "Finance:Accounts:Settings:ActiveRecordCannotBeDeleted";
	public const string DeactivationReasonRequired = "Finance:Accounts:Settings:DeactivationReasonRequired";
	public const string InvalidEffectivePeriod = "Finance:Accounts:Settings:InvalidEffectivePeriod";
	public const string AccountHeadMustBeActive = "Finance:Accounts:Settings:AccountHeadMustBeActive";
	public const string BankAccountHeadAlreadyAssigned = "Finance:Accounts:Settings:BankAccountHeadAlreadyAssigned";
	public const string ConcurrencyStampRequired = "Finance:Accounts:Settings:ConcurrencyStampRequired";
}
