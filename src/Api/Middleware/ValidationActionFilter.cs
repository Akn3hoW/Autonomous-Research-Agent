using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AutonomousResearchAgent.Api.Middleware;

public sealed class ValidationActionFilter(IServiceProvider serviceProvider) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var failures = new List<ValidationFailure>();

        foreach (var argument in context.ActionArguments.Values.Where(v => v is not null))
        {
            var validatorType = typeof(IValidator<>).MakeGenericType(argument!.GetType());
            if (serviceProvider.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContextType = typeof(ValidationContext<>).MakeGenericType(argument.GetType());
            var validationContext = (IValidationContext?)Activator.CreateInstance(validationContextType, argument);
            if (validationContext is null)
            {
                continue;
            }

            var validationResult = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
            if (validationResult.IsValid)
            {
                continue;
            }

            failures.AddRange(validationResult.Errors);
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        await next();
    }
}
