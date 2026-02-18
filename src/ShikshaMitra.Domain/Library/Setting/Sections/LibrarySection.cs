using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using ShikshaMitra.Library.Setting.Sections;

namespace ShikshaMitra.Library.Setting.Sections
{
	public class LibrarySection : AuditedAggregateRoot<Guid>, IMultiTenant
	{
		public Guid? TenantId { get; private set; }

		public Guid? AcademicYearId { get; set; }

		public string? SectionName { get; set; } // Made nullable

		public int NumberOfShelf { get; set; }

		public List<LibraryShelf> Shelves { get; set; } = new();

		protected LibrarySection() { }

		public LibrarySection(Guid id, Guid? tenantId, Guid? academicYearId, string sectionName, int numberOfShelf)
			: base(id)
		{
			TenantId = tenantId;
			AcademicYearId = academicYearId;
			SectionName = sectionName;
			NumberOfShelf = numberOfShelf;
		}
	}
}
