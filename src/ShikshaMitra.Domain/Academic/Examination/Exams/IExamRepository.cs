using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Examination.Exams;

public interface IExamRepository : IRepository<Exam, Guid>
{
}
