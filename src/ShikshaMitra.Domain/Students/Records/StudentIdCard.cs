using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Records;

public class StudentIdCard : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid? AcademicYearId { get; private set; }
	public string CardNumber { get; private set; } = "";
	public string IdempotencyKey { get; private set; } = "";
	public string Status { get; private set; } = "Issued";
	public string StudentSnapshotJson { get; private set; } = "{}";
	public string TemplateSnapshotJson { get; private set; } = "{}";
	public DateTime IssuedAt { get; private set; }
	public Guid IssuedByUserId { get; private set; }
	protected StudentIdCard() { }
	public StudentIdCard(Guid id, Guid tenant, Guid student, string number, string key, string studentSnapshot, string templateSnapshot, DateTime at, Guid actor, Guid? academicYearId = null) : base(id)
	{
		TenantId = Check.NotDefaultOrNull<Guid>(tenant, nameof(tenant)); StudentId = Check.NotDefaultOrNull<Guid>(student, nameof(student));
		CardNumber = Check.NotNullOrWhiteSpace(number, nameof(number), 80); IdempotencyKey = Check.NotNullOrWhiteSpace(key, nameof(key), 100);
		StudentSnapshotJson = studentSnapshot; TemplateSnapshotJson = templateSnapshot; IssuedAt = at; IssuedByUserId = actor; AcademicYearId = academicYearId;
	}
	public void MarkPrinted() { if (Status == "Voided") throw new BusinessException("Students:Records:DocumentVoided"); Status = "Printed"; }
	public void Void() => Status = "Voided";
}

public class StudentIdCardTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string ConfigurationJson { get; private set; } = "{}";
	protected StudentIdCardTemplate() { }
	public StudentIdCardTemplate(Guid id, Guid tenant, string json) : base(id) { TenantId = tenant; ConfigurationJson = json; }
	public void Update(string json) => ConfigurationJson = json;
}
