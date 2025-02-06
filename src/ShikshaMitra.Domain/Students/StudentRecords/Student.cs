using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Students.StudentRecords;

public class Student : FullAuditedAggregateRoot<Guid>
{
	public string Name { get; set; }

	protected Student() { }

	public Student(Guid id, string name)
		: base(id) => Name = name;
}
