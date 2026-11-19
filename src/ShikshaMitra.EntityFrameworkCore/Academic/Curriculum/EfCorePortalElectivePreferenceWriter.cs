using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ShikshaMitra.Academic.Curriculum;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Academic.Curriculum;

public class EfCorePortalElectivePreferenceWriter(IDbContextProvider<ShikshaMitraDbContext> contexts)
	: IPortalElectivePreferenceWriter, ITransientDependency
{
	public async Task AcquireAsync(Guid tenantId, Guid studentId, Guid clientRequestId)
	{
		var db = await contexts.GetDbContextAsync();
		if (!db.Database.IsNpgsql()) return;
		if (db.Database.CurrentTransaction == null) throw new BusinessException(ElectivePreferenceErrorCodes.TransactionRequired);
		// Request key protects replay across children; student key also serializes separate group requests.
		foreach (var resource in new[] { $"elective-request/{tenantId:N}/{clientRequestId:N}", $"elective-student/{tenantId:N}/{studentId:N}" })
		{
			var key = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(resource)));
			await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})");
		}
	}

	public async Task InsertAsync(StudentElectivePreferenceSubmission receipt, StudentElectiveSelection selection)
	{
		var db = await contexts.GetDbContextAsync();
		db.StudentElectivePreferenceSubmissions.Add(receipt);
		db.StudentElectiveSelections.Add(selection);
		try { await db.SaveChangesAsync(); }
		catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } ||
			error.InnerException?.GetType().Name == "SqliteException" &&
			(int?)error.InnerException.GetType().GetProperty("SqliteExtendedErrorCode")?.GetValue(error.InnerException) is 2067 or 1555)
		{
			throw new BusinessException(ElectivePreferenceErrorCodes.Conflict, innerException: error);
		}
	}
}
