using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using ClassTeacherEntity = ShikshaMitra.Academic.ClassTeacher.ClassTeacher;

namespace ShikshaMitra.Academic.Mobile;

/// <summary>Family-safe projection; callers must authorize the placement through ResolveAsync first.</summary>
public class MobileDashboardClassTeacherReader(
	IRepository<ClassTeacherEntity, Guid> assignments,
	IRepository<Employee, Guid> employees,
	ICurrentTenant tenant,
	IAsyncQueryableExecuter executer,
	ILogger<MobileDashboardClassTeacherReader> logger) : ITransientDependency
{
	public async Task<MobileClassTeacherDto?> ReadAsync(MobilePlacement placement)
	{
		var query = from assignment in await assignments.GetQueryableAsync()
			join employee in await employees.GetQueryableAsync() on assignment.TeacherId equals employee.Id
			where assignment.TenantId == tenant.Id && employee.TenantId == tenant.Id &&
				assignment.StandardId == placement.StandardId && assignment.DivisionId == placement.DivisionId &&
				assignment.AcademicMediumId == placement.MediumId && assignment.Status == ClassTeacherAssignmentStatus.Active &&
				(!assignment.AcademicYearId.HasValue || assignment.AcademicYearId == placement.Year.Id) &&
				employee.EmployeeStatusType == EmployeeStatusType.Active && employee.EmployeeGroup == EmployeeGroupType.Teaching
			select new { employee.Id, employee.FirstName, employee.LastName };
		// Only two distinct identities are needed to detect ambiguity. Project names
		// before materialization so no HR profile or contact information is loaded.
		var matches = await executer.ToListAsync(query.Distinct().Take(2));
		if (matches.Count > 1)
		{
			logger.LogWarning("Ambiguous class-teacher mapping for tenant {TenantId}, class {StandardId}, division {DivisionId}, year {AcademicYearId}",
				tenant.Id, placement.StandardId, placement.DivisionId, placement.Year.Id);
			return null;
		}
		var teacher = matches.SingleOrDefault();
		if (teacher == null) return null;
		// Matches Employee.EmployeeName, the display convention used by the web workspace.
		var name = string.Join(" ", new[] { teacher.FirstName, teacher.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
		return string.IsNullOrWhiteSpace(name) ? null : new(teacher.Id, name);
	}
}
