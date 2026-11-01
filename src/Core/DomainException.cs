namespace Core;

// Порушення бізнес-правила бібліотеки
// (не технічна помилка і не некоректний аргумент методу).
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception inner) : base(message, inner) { }
}