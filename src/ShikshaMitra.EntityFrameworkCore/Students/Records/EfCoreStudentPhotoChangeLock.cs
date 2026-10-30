using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.Records;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Students.Records;

public class EfCoreStudentPhotoChangeLock(IDbContextProvider<ShikshaMitraDbContext> provider)
	: IStudentPhotoChangeLock, ITransientDependency
{
	public async Task AcquireAsync(Guid? tenantId, Guid studentId)
	{
		var db = await provider.GetDbContextAsync();
		if (db.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL") return;
		if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Photo changes require a transaction.");
		// The no-op UPDATE is a transaction row lock and an MVCC fence. Unlike an advisory
		// lock alone, it makes a pre-existing Serializable snapshot fail after a competing
		// photo commit. This also protects admission's joined transaction without changing
		// a tracked Student's semantic stamp or introducing advisory/row lock inversion.
		await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"AppStudents\" SET \"ConcurrencyStamp\" = \"ConcurrencyStamp\" WHERE \"Id\" = {studentId} AND \"TenantId\" = {tenantId} AND NOT \"IsDeleted\"");
	}
}
