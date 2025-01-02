using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollment.Settings;

public interface IStudentDocumentSettingRepository : IRepository<StudentDocumentSetting, Guid>
{
	
}