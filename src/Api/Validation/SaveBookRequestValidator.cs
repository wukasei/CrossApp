using Api.Contracts;
using FluentValidation;

namespace Api.Validation;

// Ті самі правила, що в BookCopy.Create, але описані декларативно
public sealed class SaveBookRequestValidator : AbstractValidator<SaveBookRequest>
{
    public const int IsbnMaxLength = 20;
    public const int TitleMaxLength = 200;
    public const int AuthorMaxLength = 100;
    public const int MinYear = 1450;

    public SaveBookRequestValidator()
    {
        RuleFor(r => r.Isbn)
            .Cascade(CascadeMode.Stop)          // порожнє поле — одне повідомлення, а не два
            .NotEmpty().WithMessage("ISBN обов'язковий.")
            .MaximumLength(IsbnMaxLength).WithMessage($"ISBN — до {IsbnMaxLength} символів.");

        RuleFor(r => r.Title)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Назва обов'язкова.")
            .MaximumLength(TitleMaxLength).WithMessage($"Назва — до {TitleMaxLength} символів.");

        // null пропускається: автор необов'язковий
        RuleFor(r => r.Author)
            .MaximumLength(AuthorMaxLength).WithMessage($"Ім'я автора — до {AuthorMaxLength} символів.");

        // Поточний рік рахується під час кожної перевірки, а не один раз при створенні валідатора
        RuleFor(r => r.Year)
            .Must(year => year >= MinYear && year <= DateTime.Today.Year)
            .WithMessage(_ => $"Рік видання має бути від {MinYear} до {DateTime.Today.Year}.");
    }
}