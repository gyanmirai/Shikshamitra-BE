using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Library.Books;

public interface ILibraryInventoryLock
{
    Task AcquireAsync(Guid? tenantId);
}
