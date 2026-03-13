using ShikshaMitra.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.HR.Employees.Settings;

public class EfCoreIDocumentSettingRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, DocumentSetting, Guid>(dbContextProvider),
		IDocumentSettingRepository
{

}
