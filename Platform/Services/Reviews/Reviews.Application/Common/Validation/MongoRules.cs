using FluentValidation;
using Reviews.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Reviews.Application.Common.Validation;

internal static class MongoRules
{
    public static IRuleBuilderOptions<T, string> MustBeObjectId<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(ReviewIds.IsValid)
            .WithErrorCode("InvalidObjectId")
            .WithMessage("'{PropertyName}' має бути коректним ObjectId (24 шістнадцяткові символи).");
}
