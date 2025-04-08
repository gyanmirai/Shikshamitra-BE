
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectManager(
	IReadOnlyRepository<Subject> subjectRepository,
	IGuidGenerator guidGenerator
	) : DomainService
{
	public Subject CreateSubject(
		string subjectCode,
		string subjectName,
		Guid primaryTeacherId,
		DateTimeOffset scheduledDate,
		string subjectColor,
		string learningGoals,
		string prerequisites,
		string description)
	{
		var subject = new Subject(
			guidGenerator.Create(),
			CurrentTenant.Id,
			subjectCode,
			subjectName,
			primaryTeacherId,
			scheduledDate,
			subjectColor,
			learningGoals,
			prerequisites,
			description,
			uploadedDocument: null
		);
		return subject;
	}
}
