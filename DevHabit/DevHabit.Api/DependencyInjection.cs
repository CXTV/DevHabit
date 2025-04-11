using Asp.Versioning;
using DevHabit.Api.Services;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Serialization;
using DevHabit.Api.Middleware;
using DevHabit.Api.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Npgsql;
using OpenTelemetry;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.Entities;
using DevHabit.Api.Services.Sorting;
using FluentValidation;

namespace DevHabit.Api;

public static class DependencyInjection
{
    //服务添加啊
    public static WebApplicationBuilder AddApiServices(this WebApplicationBuilder builder)
    {
        //Contoller配置
        builder.Services.AddControllers(options =>
            {
                options.ReturnHttpNotAcceptable = true;
            })
            .AddNewtonsoftJson(options => options.SerializerSettings.ContractResolver =
                new CamelCasePropertyNamesContractResolver())
            .AddXmlSerializerFormatters();

        //自定义custom media type
        builder.Services.Configure<MvcOptions>(options =>
        {
            NewtonsoftJsonOutputFormatter formatter = options.OutputFormatters
                .OfType<NewtonsoftJsonOutputFormatter>()
                .First();

            formatter.SupportedMediaTypes.Add(CustomMediaTypeNames.Application.JsonV1);
            formatter.SupportedMediaTypes.Add(CustomMediaTypeNames.Application.JsonV2);
            formatter.SupportedMediaTypes.Add(CustomMediaTypeNames.Application.HateoasJson);
            formatter.SupportedMediaTypes.Add(CustomMediaTypeNames.Application.HateoasJsonV1);
            formatter.SupportedMediaTypes.Add(CustomMediaTypeNames.Application.HateoasJsonV2);
        });

        //api Versioning
        builder.Services
            .AddApiVersioning(
                options =>
                {
                    options.DefaultApiVersion = new ApiVersion(1, 0);
                    options.AssumeDefaultVersionWhenUnspecified = true;
                    options.ReportApiVersions = true;
                    options.ApiVersionSelector = new CurrentImplementationApiVersionSelector(options);
                    //options.ApiVersionReader = new UrlSegmentApiVersionReader();

                    //添加自定义的hateoas
                    options.ApiVersionReader = ApiVersionReader.Combine(
                        new MediaTypeApiVersionReader(),
                        new MediaTypeApiVersionReaderBuilder()
                            .Template("application/vnd.dev-habit.hateoas.{version}+json")
                            .Build());
                }
            )
            .AddMvc();


        //OpenApi
        builder.Services.AddOpenApi();

        return builder;
    }

    //错误处理配置
    public static WebApplicationBuilder AddErrorHandling(this WebApplicationBuilder builder)
    {
        //配置ProblemDetails
        builder.Services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);
            };
        });

        //全局异常处理中间件服务注册
        builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        return builder;

    }


    public static WebApplicationBuilder AddDatabase(this WebApplicationBuilder builder)
    {//数据库上下文服务注册
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options
                .UseNpgsql(
                    builder.Configuration.GetConnectionString("Database"),
                    npgsqlOptions => npgsqlOptions
                        .MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Application))
                .UseSnakeCaseNamingConvention());

        return builder;
    }

    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {

        //Aspire服务注册
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(builder.Environment.ApplicationName))
            .WithTracing(tracing => tracing
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddNpgsql())
            .WithMetrics(metrics => metrics
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation())
            .UseOtlpExporter();

        //日志服务注册
        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
        });

        return builder;
    }

    //集中注册服务
    public static WebApplicationBuilder AddApplicationServices(this WebApplicationBuilder builder)
    {
        //注册FluentValidation
        builder.Services.AddValidatorsFromAssemblyContaining<Program>();
        //注册SortMappingProvider
        builder.Services.AddTransient<SortMappingProvider>();
        //排序服务注册只有Habit
        builder.Services.AddSingleton<ISortMappingDefinition, SortMappingDefinition<HabitDto, Habit>>(_ =>
            HabitMappings.SortMapping);
        //注册数据整形服务
        builder.Services.AddTransient<DataShapingService>();
        //注册httpContextAccessor
        builder.Services.AddHttpContextAccessor();
        //注册Hateoas服务
        builder.Services.AddTransient<LinkService>();

        return builder;
    }


}
