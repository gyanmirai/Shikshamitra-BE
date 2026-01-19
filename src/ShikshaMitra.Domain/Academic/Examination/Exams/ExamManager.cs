using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.Examination.Exams;

public class ExamManager(
	IRepository<Exam, Guid> examRepository,
	IGuidGenerator guidGenerator
) : DomainService
{
    public Exam Create(Guid examTypeId, string examName, Guid standardId, List<Guid> divisionIds, List<Guid> subjectIds)
    {
        var exam = new Exam(guidGenerator.Create(), examTypeId, examName, standardId, CurrentTenant.Id);

        foreach (var divisionId in divisionIds)
        {
            exam.AddDivision(guidGenerator.Create(), divisionId);
        }

        foreach (var subjectId in subjectIds)
        {
            exam.AddSubject(new ExamSubject(guidGenerator.Create(), exam.Id, subjectId));
        }

		return exam;
    }
}
