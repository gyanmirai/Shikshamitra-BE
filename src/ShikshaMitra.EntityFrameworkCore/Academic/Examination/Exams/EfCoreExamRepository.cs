using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Examination.Exams;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Academic.Examination.Exams;

public class EfCoreExamRepository(
    IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
    : EfCoreRepository<ShikshaMitraDbContext, Exam, Guid>(dbContextProvider), IExamRepository
{
    public override async Task<IQueryable<Exam>> WithDetailsAsync()
    {
        return (await GetQueryableAsync())
            .Include(x => x.ExamDivisions)
            .Include(x => x.ExamSubjects)
                .ThenInclude(x => x.RoomAssignments);
    }
}
