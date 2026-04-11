using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Library.Setting.Sections
{
	public class LibraryShelf : Entity<Guid>
	{
		public Guid SectionId { get; set; }
		public LibrarySection Section { get; set; }

		public string? ShelfName { get; set; }

		protected LibraryShelf() { }

		public LibraryShelf(Guid id, Guid sectionId, string shelfName)
			: base(id)
		{
			SectionId = sectionId;
			ShelfName = shelfName;
		}
	}
}
