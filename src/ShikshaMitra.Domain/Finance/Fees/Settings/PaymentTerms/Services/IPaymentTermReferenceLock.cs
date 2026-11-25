using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms.Services;

public interface IPaymentTermReferenceLock
{
	Task<bool> AcquireAsync(Guid[] paymentTermIds);
}
