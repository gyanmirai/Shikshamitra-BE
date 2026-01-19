using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Academic.Examination.Exams;

public class ExamManager(
	IRepository<Exam, Guid> examRepository
) : DomainService
{
    public async Task<Exam> CreateAsync(Guid examTypeId, string examName, Guid standardId, List<Guid> divisionIds)
    {
        var exam = new Exam(GuidGenerator.Create(), examTypeId, examName, standardId, CurrentTenant.Id);

        foreach (var divisionId in divisionIds)
        {
            exam.AddDivision(GuidGenerator.Create(), divisionId);
        }

        return await examRepository.InsertAsync(exam);
    }
}
