using System.Threading.Tasks;

namespace ShikshaMitra.Domain.Finance.Accounts.Settings;

/// <summary>Coordinates category deletion and new references until their transaction commits.</summary>
public interface ITransactionCategoryReferenceLock
{
	Task AcquireAsync();
}
