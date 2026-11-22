using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

// Одиниця роботи EF Core: відстежує зміни і формує SQL під час SaveChanges.
// Реєструється як Scoped, тобто один екземпляр на HTTP-запит.
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<BookRow> Books => Set<BookRow>();
    public DbSet<CopyRow> Copies => Set<CopyRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ---------- Books ----------
        var book = modelBuilder.Entity<BookRow>();
        book.ToTable("Books");

        book.HasKey(b => b.Id);                                   // string-ключ
        book.Property(b => b.Id).HasMaxLength(32);

        book.Property(b => b.Isbn).IsRequired().HasMaxLength(32);
        book.Property(b => b.Title).IsRequired().HasMaxLength(200);
        book.Property(b => b.Author).HasMaxLength(200);           // необов'язкове (string?)
        book.Property(b => b.Year).IsRequired();

        book.HasIndex(b => b.Isbn).IsUnique();                    // унікальний ISBN

        // 1:N — книга має багато примірників; видалили книгу → видалились її примірники
        book.HasMany(b => b.Copies)
            .WithOne(c => c.Book)
            .HasForeignKey(c => c.BookId)                         // зовнішній ключ
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // ---------- Copies ----------
        var copy = modelBuilder.Entity<CopyRow>();
        copy.ToTable("Copies");

        copy.HasKey(c => c.Id);                                   // string-ключ
        copy.Property(c => c.Id).HasMaxLength(32);
        copy.Property(c => c.BookId).IsRequired().HasMaxLength(32);

        // enum зберігаємо рядком: у БД видно "Issued", а не 1
        copy.Property(c => c.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);
    }
}