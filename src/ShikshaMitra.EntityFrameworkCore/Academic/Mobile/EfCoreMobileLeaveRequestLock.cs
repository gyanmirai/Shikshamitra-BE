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

public class EfCoreMobileLeaveRequestLock(IDbContextProvider<ShikshaMitraDbContext> contexts)
    : IMobileLeaveRequestLock, ITransientDependency
{
    public async Task AcquireAsync(Guid tenantId, Guid studentId)
    {
        var db = await contexts.GetDbContextAsync();
        if (!db.Database.IsNpgsql()) return;
        if (db.Database.CurrentTransaction == null) throw new BusinessException("MobileAcademics:TransactionRequired");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"mobile-leave/{tenantId:N}/{studentId:N}"));
        var key = BinaryPrimitives.ReadInt64BigEndian(hash);
        // The transaction holds this across overlap validation and commit, including requests on other hosts.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})");
    }
}
