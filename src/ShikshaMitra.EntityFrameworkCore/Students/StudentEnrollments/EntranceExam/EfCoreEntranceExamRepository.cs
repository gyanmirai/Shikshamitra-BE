using ShikshaMitra.EntityFrameworkCore;
using System;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Students.StudentEnrollments.EntranceExam;

public class EfCoreEntranceExamRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider) : EfCoreRepository<ShikshaMitraDbContext, EntranceExam, Guid>(dbContextProvider), IEntranceExamRepository
{
}