using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Accounts.CashManagement;

public interface ICashCollectionLock
{
	Task AcquireAsync(Guid sourceId);
}
