using System.Text;
using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Analysis;
using AutonomousResearchAgent.Api.Contracts.Jobs;
using AutonomousResearchAgent.Api.Contracts.Papers;
using AutonomousResearchAgent.Api.Contracts.Search;
using AutonomousResearchAgent.Api.Contracts.Summaries;
using AutonomousResearchAgent.Api.Middleware;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace AutonomousResearchAgent.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiLayer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddProblemDetails();

        services.AddScoped<ValidationActionFilter>();

        services.AddControllers(options =>
        {
            options.Filters.Add(new ServiceFilterAttribute(typeof(ValidationActionFilter)));
        });

        services.AddJwtAuthentication(configuration);
        services.AddAuthorizationPolicies();
        services.AddApiValidators();

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = jwtOptions.RequireHttpsMetadata;

                if (!string.IsNullOrWhiteSpace(jwtOptions.Authority))
                {
                    options.Authority = jwtOptions.Authority;
                    options.Audience = jwtOptions.Audience;
                }
                else
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = !string.IsNullOrWhiteSpace(jwtOptions.Issuer),
                        ValidIssuer = jwtOptions.Issuer,
                        ValidateAudience = !string.IsNullOrWhiteSpace(jwtOptions.Audience),
                        ValidAudience = jwtOptions.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(2)
                    };
                }
            });

        return services;
    }

    public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(PolicyNames.ReadAccess, policy => policy.RequireRole(RoleNames.Admin, RoleNames.Editor, RoleNames.Reviewer, RoleNames.ReadOnly));
            options.AddPolicy(PolicyNames.EditAccess, policy => policy.RequireRole(RoleNames.Admin, RoleNames.Editor));
            options.AddPolicy(PolicyNames.ReviewAccess, policy => policy.RequireRole(RoleNames.Admin, RoleNames.Reviewer));
            options.AddPolicy(PolicyNames.AdminAccess, policy => policy.RequireRole(RoleNames.Admin));
        });

        return services;
    }

    public static IServiceCollection AddApiValidators(this IServiceCollection services)
    {
        var assembly = typeof(ServiceCollectionExtensions).Assembly;
        var validatorRegistrations = assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type
                .GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>))
                .Select(i => new { ServiceType = i, ImplementationType = type }));

        foreach (var registration in validatorRegistrations)
        {
            services.AddScoped(registration.ServiceType, registration.ImplementationType);
        }

        return services;
    }

    public static IServiceCollection AddHealthAndOpenApi(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddOpenApi();
        services.AddHealthChecks();
        return services;
    }
}
