using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Library.Books;

public class EfCoreLibraryInventoryLock(IDbContextProvider<ShikshaMitraDbContext> provider)
    : ILibraryInventoryLock, ITransientDependency
{
    public async Task AcquireAsync(Guid? tenantId)
    {
        var db = await provider.GetDbContextAsync();
        if (db.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL") return;
        if (db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Library inventory changes require a transactional unit of work.");

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"library-inventory:{tenantId:N}"));
        var key = BinaryPrimitives.ReadInt64BigEndian(digest);
        // Allocation, copy edits and checkout share the same tenant transaction fence.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})");
    }
}
