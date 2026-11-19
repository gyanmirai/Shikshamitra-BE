using System;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms.Services;

/// <summary>Serializes payment-term identity changes until the current transaction ends.</summary>
public interface IPaymentTermNameMutationLock
{
	Task AcquireAsync(Guid academicYearId, CancellationToken cancellationToken = default);
}
