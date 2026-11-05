using Microsoft.EntityFrameworkCore;
using ShikshaMitra.HR.WorkShifts;
namespace ShikshaMitra.EntityFrameworkCore;
public partial class ShikshaMitraDbContext
{
	public DbSet<EmployeeWorkShift> EmployeeWorkShifts { get; set; }
	public DbSet<EmployeeShiftAssignment> EmployeeShiftAssignments { get; set; }
}