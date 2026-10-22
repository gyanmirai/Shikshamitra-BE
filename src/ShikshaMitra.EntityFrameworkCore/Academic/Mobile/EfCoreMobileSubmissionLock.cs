using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Academic.Mobile;

public class EfCoreMobileSubmissionLock(IDbContextProvider<ShikshaMitraDbContext> contexts)
	: IMobileSubmissionLock, ITransientDependency
{
	public async Task AcquireAsync(Guid tenantId, Guid studentId, Guid taskId)
	{
		var db = await contexts.GetDbContextAsync();
		if (!db.Database.IsNpgsql()) return;
		if (db.Database.CurrentTransaction == null) throw new BusinessException(MobileSubmissionErrorCodes.TransactionRequired);
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"mobile-submission/{tenantId:N}/{studentId:N}/{taskId:N}"));
		var key = BinaryPrimitives.ReadInt64BigEndian(hash);
		await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})");
	}
}
