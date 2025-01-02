using ShikshaMitra.EntityFrameworkCore;
using System;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Students.StudentEnrollment.Settings;

public class EfCoreInquiryPreferenceRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider) : EfCoreRepository<ShikshaMitraDbContext,InquiryPreference, Guid>(dbContextProvider), IInquiryPreferenceRepository
{
	
}