using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Finance.Accounts.CashManagement;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<CashCollection> CashCollections { get; set; }
}
