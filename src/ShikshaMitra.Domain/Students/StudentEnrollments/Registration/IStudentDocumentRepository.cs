using ShikshaMitra.Students.StudentEnrollments.Registration;
using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public interface IStudentDocumentRepository : IRepository<StudentDocument, Guid>
{

}