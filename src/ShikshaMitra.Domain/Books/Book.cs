using System;

namespace ShikshaMitra.Books;

public class Book
{
    public Guid AuthorId { get; set; }

    public string Name { get; set; }

    public BookType Type { get; set; }

    public DateTime PublishDate { get; set; }

    public float Price { get; set; }
}
