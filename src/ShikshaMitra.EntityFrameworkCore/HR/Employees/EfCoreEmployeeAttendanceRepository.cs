using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
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
	private const int PostgreSqlInsertBatchSize = 500;

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
		foreach (var batch in attendances.Chunk(PostgreSqlInsertBatchSize))
		{
			affectedRows += await ExecutePostgreSqlInsertBatchAsync(dbContext, batch, now, token);
		}

		return affectedRows;
	}

	private async Task<int> ExecutePostgreSqlInsertBatchAsync(
		ShikshaMitraDbContext dbContext,
		IReadOnlyList<EmployeeAttendance> attendances,
		DateTime now,
		CancellationToken cancellationToken)
	{
		var values = new List<string>(attendances.Count);
		var parameters = new List<NpgsqlParameter>(attendances.Count * 10);

		for (var i = 0; i < attendances.Count; i++)
		{
			var attendance = attendances[i];
			var id = attendance.Id == Guid.Empty ? guidGenerator.Create() : attendance.Id;
			values.Add(
				$"(@p{i}_id, @p{i}_tenant_id, @p{i}_employee_id, @p{i}_attendance_date, @p{i}_time_in, @p{i}_time_out, @p{i}_status_type, @p{i}_education_medium_stage_id, @p{i}_remarks, @p{i}_creation_time, NULL, FALSE)");

			parameters.Add(new NpgsqlParameter<Guid>($"p{i}_id", id));
			parameters.Add(new NpgsqlParameter<Guid?>($"p{i}_tenant_id", attendance.TenantId));
			parameters.Add(new NpgsqlParameter<Guid>($"p{i}_employee_id", attendance.EmployeeId));
			parameters.Add(new NpgsqlParameter<DateOnly>($"p{i}_attendance_date", attendance.AttendanceDate)
			{
				NpgsqlDbType = NpgsqlDbType.Date
			});
			parameters.Add(new NpgsqlParameter<DateTimeOffset?>($"p{i}_time_in", attendance.TimeIn)
			{
				NpgsqlDbType = NpgsqlDbType.TimestampTz
			});
			parameters.Add(new NpgsqlParameter<DateTimeOffset?>($"p{i}_time_out", attendance.TimeOut)
			{
				NpgsqlDbType = NpgsqlDbType.TimestampTz
			});
			parameters.Add(new NpgsqlParameter<string>($"p{i}_status_type", attendance.StatusType.ToString()));
			parameters.Add(new NpgsqlParameter<Guid?>($"p{i}_education_medium_stage_id", attendance.EducationMediumStageId));
			parameters.Add(new NpgsqlParameter<string?>($"p{i}_remarks", attendance.Remarks));
			parameters.Add(new NpgsqlParameter<DateTime>($"p{i}_creation_time", now)
			{
				NpgsqlDbType = NpgsqlDbType.TimestampTz
			});
		}

		var sql = $"""
			INSERT INTO "AppEmployeeAttendances"
				("Id", "TenantId", "EmployeeId", "AttendanceDate", "TimeIn", "TimeOut", "StatusType", "EducationMediumStageId", "Remarks", "CreationTime", "CreatorId", "IsDeleted")
			VALUES
				{string.Join(",\n\t", values)}
			ON CONFLICT ("TenantId", "EmployeeId", "AttendanceDate") DO NOTHING
			""";

		return await dbContext.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
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
