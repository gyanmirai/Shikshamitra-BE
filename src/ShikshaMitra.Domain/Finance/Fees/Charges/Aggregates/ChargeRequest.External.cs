using System;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.Charges;

public partial class ChargeRequest
{
	private ChargeRequest(Guid id) : base(id) { }

	public static ChargeRequest CreateExternal(Guid id, Guid tenantId, Guid yearId, string name,
		Guid categoryId, string reason, decimal amount, DateOnly dueDate, Guid requestorId, string sourceSnapshot)
	{
		if (id == Guid.Empty || tenantId == Guid.Empty || yearId == Guid.Empty || categoryId == Guid.Empty ||
			requestorId == Guid.Empty || dueDate == default || string.IsNullOrWhiteSpace(sourceSnapshot))
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource);
		var request = new ChargeRequest(id)
		{
			TenantId = tenantId, AcademicYearId = yearId,
			ChargeName = Check.NotNullOrWhiteSpace(name, nameof(name), MaxChargeNameLength),
			ChargeCategoryId = categoryId, PurposeDescription = Check.NotNullOrWhiteSpace(reason, nameof(reason)),
			Amount = amount, SchoolDueDate = dueDate,
			DueDateProvenance = FinanceCivilDateProvenance.ConfirmedExternalSource,
			Priority = ChargePriority.Medium, RequestorId = requestorId,
			AppliedOn = DateTimeOffset.UtcNow, AudienceSnapshot = sourceSnapshot
		};
		request.ValidateAmount();
		return request;
	}
}
