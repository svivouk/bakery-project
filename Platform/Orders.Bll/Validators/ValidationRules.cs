using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Orders.Bll.Validators;

internal static partial class ValidationRules
{
    public static bool HasMaxTwoDecimals(decimal value) => decimal.Round(value, 2) == value;
    public static bool IsValidPromoCode(string code) => PromoCodePattern().IsMatch(code.Trim());
    public static bool IsBase64(string value)
    {
        try
        {
            _ = Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    [GeneratedRegex(@"^[\p{L}\p{N}_-]+$")]
    private static partial Regex PromoCodePattern();
}
