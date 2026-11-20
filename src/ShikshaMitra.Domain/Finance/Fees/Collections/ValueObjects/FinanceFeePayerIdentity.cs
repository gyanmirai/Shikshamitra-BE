using System;
using ShikshaMitra.Finance.Fees.Collections;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>Checks real student identity and the explicit pre-enrollment Finance payer.</summary>
public static class FinanceFeePayerIdentity
{
	public static void Validate(Guid? studentId, Guid? financeFeeAccountId, Guid? employeeId = null)
	{
		if (studentId == Guid.Empty || financeFeeAccountId == Guid.Empty || employeeId == Guid.Empty ||
			employeeId.HasValue && (studentId.HasValue || financeFeeAccountId.HasValue) ||
			!employeeId.HasValue && !studentId.HasValue && !financeFeeAccountId.HasValue)
			throw new BusinessException(FinanceFeeAccountErrorCodes.InvalidPayer);
	}

	public static Guid LinkStudent(Guid? currentStudentId, Guid studentId, Guid? employeeId = null)
	{
		if (studentId == Guid.Empty || employeeId.HasValue)
			throw new BusinessException(FinanceFeeAccountErrorCodes.InvalidPayer);
		if (currentStudentId.HasValue && currentStudentId.Value != studentId)
			throw new BusinessException(FinanceFeeAccountErrorCodes.StudentLinkConflict);
		return studentId;
	}

	public static Guid BindAccount(Guid? currentAccountId, Guid accountId)
	{
		if (accountId == Guid.Empty)
			throw new BusinessException(FinanceFeeAccountErrorCodes.InvalidPayer);
		if (currentAccountId.HasValue && currentAccountId.Value != accountId)
			throw new BusinessException(FinanceFeeAccountErrorCodes.AssignmentConflict);
		return accountId;
	}
}
