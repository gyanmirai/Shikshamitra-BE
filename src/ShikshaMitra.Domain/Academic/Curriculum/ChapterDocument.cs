using System;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Academic.Curriculum
{
	public class ChapterDocument : AggregateRoot<Guid>
	{
		#region Properties

		public Guid ChapterId { get; set; }

		[Column(TypeName = "text")] 
		public string Base64Content { get; set; }

		#endregion

		#region Constructors

		protected ChapterDocument()
		{
			
		}

		public ChapterDocument(Guid chapterId, string base64Content)
		{
			Id = Guid.NewGuid();
			Check.NotNull(chapterId, nameof(ChapterId));
			Check.NotNullOrWhiteSpace(base64Content, nameof(Base64Content));
		
			ChapterId = chapterId;
			Base64Content = base64Content;
		}
		#endregion
	}
}
