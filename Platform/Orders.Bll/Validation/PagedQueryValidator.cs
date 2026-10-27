using FluentValidation;
using Orders.Bll.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Bll.Validation;

public abstract class PagedQueryValidator<TQuery> : AbstractValidator<TQuery> where TQuery : PagedQuery
{
    protected PagedQueryValidator(IReadOnlyCollection<string> sortableFields)
    {
        RuleFor(q => q).Custom((q, context) =>
        {
            if (SortParser.TryParse(q.Sort, q.SortBy, q.SortDir, sortableFields, out _, out var error)) return;
            var member = !string.IsNullOrWhiteSpace(q.Sort) ? nameof(PagedQuery.Sort)
                : !string.IsNullOrWhiteSpace(q.SortBy) ? nameof(PagedQuery.SortBy)
                : nameof(PagedQuery.SortDir);
            context.AddFailure(member, error!);
        });
    }
}

