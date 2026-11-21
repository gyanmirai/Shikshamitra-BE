using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Finance.ExternalReceivables;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<FinanceExternalReceivableSourceLink> FinanceExternalReceivableSourceLinks { get; set; } = null!;
	public DbSet<FinanceExternalReceivableCommand> FinanceExternalReceivableCommands { get; set; } = null!;
	public DbSet<FinanceExternalSettlementPolicy> FinanceExternalSettlementPolicies { get; set; } = null!;
	public DbSet<FinanceExternalWaiverRequest> FinanceExternalWaiverRequests { get; set; } = null!;
	public DbSet<FinanceChargeWaiverReversal> FinanceChargeWaiverReversals { get; set; } = null!;
}
