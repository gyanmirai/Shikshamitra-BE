using System;

namespace ShikshaMitra.Finance.Fees.Settings;

internal static class FeeSetupPersistenceErrors
{
	public static bool IsCategoryNameConflict(Exception exception) =>
		IsUniqueConstraint(exception, "Finance_FeeCategories", "IX_Finance_FeeCategories");

	public static bool IsFeeHeadNameConflict(Exception exception) =>
		IsUniqueConstraint(exception, "Finance_FeeHeads", "IX_Finance_FeeHeads_TenantId_AcademicYearId_FeeCategoryId_NormalizedName");

	private static bool IsUniqueConstraint(Exception exception, params string[] markers)
	{
		var current = exception;
		var isDatabaseUpdate = false;
		while (current != null)
		{
			isDatabaseUpdate |= string.Equals(current.GetType().Name, "DbUpdateException", StringComparison.Ordinal);
			if (isDatabaseUpdate &&
				(current.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
				 current.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)) &&
				 Array.Exists(markers, marker => current.Message.Contains(marker, StringComparison.OrdinalIgnoreCase)))
				return true;
			current = current.InnerException;
		}
		return false;
	}
}
