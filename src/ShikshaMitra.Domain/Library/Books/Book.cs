using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Books;

public class Book : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid? AcademicYearId { get; set; }

    public string Title { get; set; }

    public string Isbn { get; set; }

    public string Author { get; set; }

    public string Publisher { get; set; }

    public string Edition { get; set; }

    public string Subject { get; set; }

    public Guid CategoryId { get; set; }

    public Language Language { get; set; }

    public string ApplicableAge { get; set; }

    public decimal Price { get; set; }

    public int Pages { get; set; }

    public int NumberOfCopies { get; set; }

    public List<BookCopy> Copies { get; set; } = new();

    protected Book() { }

    public Book(
        string title,
        string isbn,
        string author,
        string publisher,
        string edition,
        string subject,
        Guid categoryId,
        Language language,
        string applicableAge,
        decimal price,
        int pages,
        int numberOfCopies,
        Guid? academicYearId = null,
        Guid? tenantId = null)
    {
        Title = title;
        Isbn = isbn;
        Author = author;
        Publisher = publisher;
        Edition = edition;
        Subject = subject;
        CategoryId = categoryId;
        Language = language;
        ApplicableAge = applicableAge;
        Price = price;
        Pages = pages;
        NumberOfCopies = numberOfCopies;
        AcademicYearId = academicYearId;
        TenantId = tenantId;
    }
}
