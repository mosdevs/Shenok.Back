using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Shenok.Back.Api.Shared.Errors;

namespace Shenok.Back.Api.Shared.Validation;

public sealed class ValidationFilter : IAsyncActionFilter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
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

            var errors = result.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(
                    group => string.IsNullOrEmpty(group.Key) ? "_" : group.Key,
                    group => group.Select(error => error.ErrorMessage).Distinct().ToArray());

            var problem = new ApiProblem(
                "https://shenok.dev/problems/validation",
                "Validation failed",
                StatusCodes.Status400BadRequest,
                "One or more fields are invalid.",
                context.HttpContext.TraceIdentifier,
                errors);

            context.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.HttpContext.Response.ContentType = "application/problem+json";
            await context.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
            return;
        }

        await next();
    }
}
