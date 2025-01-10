using ShikshaMitra.EntityFrameworkCore;
using System;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class EfCoreStudentDocumentSettingRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider) : EfCoreRepository<ShikshaMitraDbContext, StudentDocumentSetting, Guid>(dbContextProvider), IStudentDocumentSettingRepository
{
	
}