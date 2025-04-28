using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace DevHabit.Api.Services;


[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentRequestAttribute : Attribute, IAsyncActionFilter //IAsyncActionFilter，所以能在请求执行前后插入逻辑
{
    //定义请求头字段的名字，就是客户端发送时需要带这个 Idempotency-Key
    private const string IdempotenceKeyHeader = "Idempotency-Key"; 
    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(60); //定义默认的缓存时长60mins

    //实现 IAsyncActionFilter 的方法：请求进来时执行
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // 检查请求头中是否有 Idempotency-Key 字段
        if (!context.HttpContext.Request.Headers.TryGetValue(
                IdempotenceKeyHeader,
                out StringValues idempotenceKeyValue) ||
            !Guid.TryParse(idempotenceKeyValue, out Guid idempotenceKey))
        {
            ProblemDetailsFactory problemDetailsFactory = context.HttpContext.RequestServices
                .GetRequiredService<ProblemDetailsFactory>();

            ProblemDetails problemDetails = problemDetailsFactory.CreateProblemDetails(
                context.HttpContext,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: $"Invalid or missing {IdempotenceKeyHeader} header");

            context.Result = new BadRequestObjectResult(problemDetails);
            return;
        }

        //通过依赖注入拿到内存缓存（IMemoryCache）
        IMemoryCache cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
        //通过 Idempotency-Key 生成缓存的键
        string cacheKey = $"IdempotentRequest:{idempotenceKey}";
        //尝试从缓存里拿出之前保存的状态码（比如之前请求成功返回了 200）
        int? statusCode = cache.Get<int?>(cacheKey);
        //如果缓存中有记录，说明这个请求之前处理过了,直接返回上次的 HTTP 状态码
        if (statusCode is not null)
        {
            var result = new StatusCodeResult(statusCode.Value);
            context.Result = result;
            return;
        }
        //如果缓存中没有记录，说明这个请求是第一次处理
        ActionExecutedContext executedContext = await next();
        //执行完请求后,保存状态码到缓存中，供以后相同的 Idempotency-Key 复用
        if (executedContext.Result is ObjectResult objectResult)
        {
            cache.Set(cacheKey, objectResult.StatusCode, DefaultCacheDuration);
        }
    }
}
