using System;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.Charges;

public partial class Charge
{
	private Charge(Guid id) : base(id) { PurposeDescription = null!; }

	public static Charge CreateExternal(Guid id, Guid tenantId, Guid yearId, Guid requestId,
		string name, Guid categoryId, FinanceExternalPayerIdentity payer, string reason, decimal amount,
		DateOnly assessmentDate, DateOnly dueDate, Guid requestorId)
	{
		if (id == Guid.Empty || tenantId == Guid.Empty || yearId == Guid.Empty || requestId == Guid.Empty ||
			categoryId == Guid.Empty || requestorId == Guid.Empty || assessmentDate == default || dueDate < assessmentDate)
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource);
		var charge = new Charge(id)
		{
			TenantId = tenantId, AcademicYearId = yearId, ChargeRequestId = requestId,
			ChargeName = Check.NotNullOrWhiteSpace(name, nameof(name)), ChargeCategoryId = categoryId,
			StudentId = payer.StudentId, EmployeeId = payer.EmployeeId,
			PurposeDescription = Check.NotNullOrWhiteSpace(reason, nameof(reason)), Amount = amount,
			SchoolAssessmentDate = assessmentDate, SchoolDueDate = dueDate,
			DueDateProvenance = FinanceCivilDateProvenance.ConfirmedExternalSource,
			AppliedOn = DateTimeOffset.UtcNow, Priority = ChargePriority.Medium,
			Status = ChargeStatus.Pending, RequestorId = requestorId
		};
		charge.ValidateAmount();
		return charge;
	}
}
