using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Guids;

namespace ShikshaMitra.Employees;

public class EfCoreEmployeeAttendanceRepository(
	IDbContextProvider<ShikshaMitraDbContext> dbContextProvider,
	IGuidGenerator guidGenerator)
	: EfCoreRepository<ShikshaMitraDbContext, EmployeeAttendance, Guid>(dbContextProvider),
		IEmployeeAttendanceRepository
{
	// override the GetListAsync method to include the Employee navigation property
	public override async Task<List<EmployeeAttendance>> GetListAsync(
		Expression<Func<EmployeeAttendance, bool>> predicate,
		bool includeDetails = false,
		CancellationToken cancellationToken = default)
	{
		// if includeDetails is true, include the Employee navigation property
		if (includeDetails)
		{
			return await (await GetQueryableAsync())
				.Include(x => x.Employee)
				.Where(predicate)
				.ToListAsync(GetCancellationToken(cancellationToken));
		}

		return await base.GetListAsync(predicate, includeDetails, cancellationToken);
	}

	public async Task<int> InsertManyIgnoringDuplicatesAsync(
		IReadOnlyCollection<EmployeeAttendance> attendances,
		CancellationToken cancellationToken = default)
	{
		if (attendances.Count == 0)
		{
			return 0;
		}

		var dbContext = await GetDbContextAsync();
		var token = GetCancellationToken(cancellationToken);
		if (dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) != true)
		{
			var insertedCount = 0;
			foreach (var attendance in attendances)
			{
				var exists = await dbContext.Set<EmployeeAttendance>().AnyAsync(x =>
					x.TenantId == attendance.TenantId &&
					x.EmployeeId == attendance.EmployeeId &&
					x.AttendanceDate == attendance.AttendanceDate,
					token);
				if (exists)
				{
					continue;
				}

				EnsureId(attendance);
				await dbContext.Set<EmployeeAttendance>().AddAsync(attendance, token);
				insertedCount++;
			}

			if (insertedCount > 0)
			{
				await dbContext.SaveChangesAsync(token);
			}

			return insertedCount;
		}

		var affectedRows = 0;
		var now = DateTime.UtcNow;
		foreach (var attendance in attendances)
		{
			var id = attendance.Id == Guid.Empty ? guidGenerator.Create() : attendance.Id;
			affectedRows += await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
				INSERT INTO "AppEmployeeAttendances"
					("Id", "TenantId", "EmployeeId", "AttendanceDate", "TimeIn", "TimeOut", "StatusType", "EducationMediumStageId", "Remarks", "CreationTime", "CreatorId", "IsDeleted")
				VALUES
					({id}, {attendance.TenantId}, {attendance.EmployeeId}, {attendance.AttendanceDate}, {attendance.TimeIn}, {attendance.TimeOut}, {attendance.StatusType.ToString()}, {attendance.EducationMediumStageId}, {attendance.Remarks}, {now}, NULL, FALSE)
				ON CONFLICT ("TenantId", "EmployeeId", "AttendanceDate") DO NOTHING
				""", token);
		}

		return affectedRows;
	}

	private void EnsureId(EmployeeAttendance attendance)
	{
		if (attendance.Id != Guid.Empty)
		{
			return;
		}

		var setter = typeof(Volo.Abp.Domain.Entities.Entity<Guid>)
			.GetProperty(nameof(Volo.Abp.Domain.Entities.Entity<Guid>.Id))!
			.GetSetMethod(nonPublic: true)!;
		setter.Invoke(attendance, new object[] { guidGenerator.Create() });
	}
}
