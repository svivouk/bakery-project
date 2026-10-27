namespace Catalog.Bll.Dto;

///  Шаблони повідомлень для DataAnnotations ({0} — ім'я поля, {1}/{2} — межі). 
internal static class Msg
{
    public const string Required = "Поле '{0}' обов'язкове.";
    public const string MaxLength = "Поле '{0}' не може бути довшим за {1} символів.";
    public const string Range = "Поле '{0}' має бути в діапазоні від {1} до {2}.";
    public const string Slug = "Поле '{0}' може містити лише малі латинські літери, цифри та дефіс.";
    public const string RowVersion = "Поле '{0}' обов'язкове (8 байтів, base64) — токен конкурентності.";

    public const string SlugPattern = "^[a-z0-9-]+$";
    public const double MaxPrice = 99_999_999.99;
}
