using ShikshaMitra.TenantSettings.EducationMediums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Examination.Setting
{
	public class GradeScale : FullAuditedAggregateRoot<Guid>, IMultiTenant
	{
		public Guid? TenantId { get; private set; }
		public string Title { get; private set; }
		public int TotalMarks { get; private set; }
		public Guid EducationMediumId { get; private set; }
		public EducationMedium EducationMedium { get; private set; }
		public ICollection<GradeRange> GradeRanges { get; private set; } = new List<GradeRange>();

		private GradeScale() { }

		public GradeScale(
			Guid id,
			Guid? tenantId,
			string title,
			int totalMarks,
			Guid educationMediumId
		) : base(id)
		{
			TenantId = tenantId;
			SetTitle(title);
			SetTotalMarks(totalMarks);
			SetEducationMedium(educationMediumId);
		}

		public void SetTitle(string title)
		{
			Check.NotNullOrWhiteSpace(title, nameof(title));
			Title = title.Trim();
		}

		public void SetTotalMarks(int totalMarks)
		{
			if (totalMarks <= 0)
				throw new BusinessException("GradeScale:TotalMarksInvalid")
					.WithData("TotalMarks", totalMarks);

			TotalMarks = totalMarks;
		}

		public void SetEducationMedium(Guid educationMediumId)
		{
			if (educationMediumId == Guid.Empty)
				throw new BusinessException("GradeScale:EducationMediumRequired");

			EducationMediumId = educationMediumId;
		}

		public void AddGradeRange(GradeRange gradeRange)
		{
			Check.NotNull(gradeRange, nameof(gradeRange));
			GradeRanges.Add(gradeRange);
		}
	}
}
