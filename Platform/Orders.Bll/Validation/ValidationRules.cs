using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Bll.Validation;

public static class ValidationRules
{
    public static bool HasAtMostTwoDecimals(decimal value) => decimal.Round(value, 2) == value;
    public static bool HasTimeZone(DateTime value) => value.Kind != DateTimeKind.Unspecified;

    public static bool IsBase64(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        var buffer = new byte[value.Length];
        return Convert.TryFromBase64String(value, buffer, out _);
    }

    public static IRuleBuilderOptions<T, decimal> HaveAtMostTwoDecimals<T>(this IRuleBuilder<T, decimal> rule, string field)
        => rule.Must(HasAtMostTwoDecimals).WithMessage($"{field}: допускається щонайбільше 2 знаки після коми.");
}

