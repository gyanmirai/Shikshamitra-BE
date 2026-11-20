using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Library.InventoryVerification;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<LibraryStockVerificationSession> LibraryStockVerificationSessions { get; set; } = null!;
	public DbSet<LibraryStockVerificationBaseline> LibraryStockVerificationBaselines { get; set; } = null!;
	public DbSet<LibraryStockVerificationObservation> LibraryStockVerificationObservations { get; set; } = null!;
	public DbSet<LibraryStockVerificationCommand> LibraryStockVerificationCommands { get; set; } = null!;
	public DbSet<LibraryStockVerificationDecision> LibraryStockVerificationDecisions { get; set; } = null!;
	public DbSet<LibraryStockVerificationResult> LibraryStockVerificationResults { get; set; } = null!;
}
