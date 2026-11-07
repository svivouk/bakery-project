using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Orders.Api.Middleware;

public static class ControllerValidationExtensions
{
    public static async Task<ActionResult?> ValidateOrProblemAsync<T>(
        this ControllerBase controller, IValidator<T> validator, T model, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(model, ct);
        if (result.IsValid) return null;

        foreach (var e in result.Errors)
            controller.ModelState.AddModelError(e.PropertyName, e.ErrorMessage);

        return controller.ValidationProblem(controller.ModelState);
    }
}
