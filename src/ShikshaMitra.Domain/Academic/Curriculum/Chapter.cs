using ShikshaMitra.Academic.Curriculum.Setting;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum
{
	public class Chapter : FullAuditedAggregateRoot<Guid>, IMultiTenant
	{
		#region Properties

		public Guid? TenantId { get; private set; }

		public Guid StandardId { get; set; }

		public virtual Standard Standard { get; set; } = default!;

		public Guid SubjectId { get; set; }

		public virtual Subject Subject { get; set; } = default!;

		public int ChapterNumber { get; set; }

		public string Name { get; set; } = default!;

		public DateTimeOffset StartDate { get; set; }

		public DateTimeOffset EndDate { get; set; }

		public string? Description { get; set; }

		public ICollection<ChapterDocument>? Documents { get; set; } = new HashSet<ChapterDocument>();

		#endregion

		#region Constructors

		protected Chapter() { }

		public Chapter(
			Guid id,
			Guid? tenantId,
			Guid standardId,
			Guid subjectId,
			int number,
			string name,
			DateTimeOffset startDate,
			DateTimeOffset endDate,
			string? description = null
		) : base(id)
		{
			Check.NotNullOrEmpty(name, nameof(name));
			Check.Range(number, nameof(number), 1, int.MaxValue);

			TenantId = tenantId;
			StandardId = standardId;
			SubjectId = subjectId;
			ChapterNumber = number;
			Name = name;
			StartDate = startDate;
			EndDate = endDate;
			Description = description;
		}

		#endregion

		#region Methods

		public void UpdateDetails(
			string name,
			DateTimeOffset startDate,
			DateTimeOffset endDate,
			string? description = null
		)
		{
			Check.NotNullOrEmpty(name, nameof(name));

			Name = name;
			StartDate = startDate;
			EndDate = endDate;
			Description = description;
		}

		public void AddDocument(ChapterDocument document)
		{
			Check.NotNull(document, nameof(document));
			Documents.Add(document);
		}

		public void RemoveDocument(ChapterDocument document)
		{
			Check.NotNull(document, nameof(document));
			Documents.Remove(document);
		}

		#endregion
	}
}
