using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using Shenok.Back.Api.Shared.Errors;

namespace Shenok.Back.Api.Shared.Validation;

public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid)
        {
            await ApiProblems.WriteAsync(
                context.HttpContext,
                ApiProblems.Validation(
                    context.HttpContext.TraceIdentifier,
                    ApiProblems.FromModelState(context.ModelState)));
            return;
        }

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
                continue;

            var validationContext = (IValidationContext)Activator.CreateInstance(
                typeof(ValidationContext<>).MakeGenericType(argument.GetType()),
                argument)!;

            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
            if (result.IsValid)
                continue;

            await ApiProblems.WriteAsync(
                context.HttpContext,
                ApiProblems.Validation(
                    context.HttpContext.TraceIdentifier,
                    ApiProblems.Group(result.Errors)));
            return;
        }

        await next();
    }
}
