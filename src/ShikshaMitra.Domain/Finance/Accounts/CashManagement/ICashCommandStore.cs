using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Accounts.CashManagement;

/// <summary>Appends replay evidence in the cash transaction without changing its saved version.</summary>
public interface ICashCommandStore
{
	Task AppendAsync(Guid sourceId, Guid commandId, string fingerprint, string resultJson);
}
