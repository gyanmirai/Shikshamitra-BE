namespace ShikshaMitra.Finance.ExternalReceivables;

internal static class FinancePayerConstraintSql
{
	public const string StudentAccountOrEmployee = "((\"EmployeeId\" IS NULL AND (\"StudentId\" IS NOT NULL OR \"FinanceFeeAccountId\" IS NOT NULL)) OR (\"EmployeeId\" IS NOT NULL AND \"StudentId\" IS NULL AND \"FinanceFeeAccountId\" IS NULL))";
	public const string StudentOrEmployee = "((\"StudentId\" IS NOT NULL AND \"EmployeeId\" IS NULL) OR (\"StudentId\" IS NULL AND \"EmployeeId\" IS NOT NULL))";
	public const string OptionalEmployee = "(\"EmployeeId\" IS NULL OR (\"StudentId\" IS NULL AND \"FinanceFeeAccountId\" IS NULL))";
}
