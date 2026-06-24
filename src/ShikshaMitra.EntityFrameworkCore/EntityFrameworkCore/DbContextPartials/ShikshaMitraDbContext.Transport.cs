using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Transport;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<TransportStop> TransportStops { get; set; }
	public DbSet<TransportRoute> TransportRoutes { get; set; }
	public DbSet<TransportRouteStop> TransportRouteStops { get; set; }
	public DbSet<TransportVehicleType> TransportVehicleTypes { get; set; }
	public DbSet<TransportRequiredVehicleDocument> TransportRequiredVehicleDocuments { get; set; }
	public DbSet<TransportServiceType> TransportServiceTypes { get; set; }
	public DbSet<TransportFuelType> TransportFuelTypes { get; set; }
	public DbSet<TransportVehicle> TransportVehicles { get; set; }
	public DbSet<TransportVehicleDocument> TransportVehicleDocuments { get; set; }
	public DbSet<TransportVehicleCompliance> TransportVehicleCompliances { get; set; }
	public DbSet<TransportVehicleMaintenanceRecord> TransportVehicleMaintenanceRecords { get; set; }
	public DbSet<TransportStaff> TransportStaff { get; set; }
	public DbSet<TransportRouteAssignment> TransportRouteAssignments { get; set; }
	public DbSet<TransportMember> TransportMembers { get; set; }
	public DbSet<TransportApplication> TransportApplications { get; set; }
	public DbSet<TransportMemberRouteAssignment> TransportMemberRouteAssignments { get; set; }
	public DbSet<TransportZoneArea> TransportZoneAreas { get; set; }
	public DbSet<TransportFeeConfiguration> TransportFeeConfigurations { get; set; }
	public DbSet<TransportFeeRun> TransportFeeRuns { get; set; }
	public DbSet<TransportFeeDue> TransportFeeDues { get; set; }
}
