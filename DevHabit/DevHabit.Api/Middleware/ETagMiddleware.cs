using System.Security.Cryptography;
using System.Text;
using DevHabit.Api.Services;

namespace DevHabit.Api.Middleware;

//定义一个中间件，表示请求管道中的下一个中间件
public sealed class ETagMiddleware(RequestDelegate next)
{
    //乐观锁校验方法列表，包含 PUT 和 PATCH 方法
    private static readonly string[] ConcurrencyCheckMethods =
        [
            HttpMethods.Put,
            HttpMethods.Patch,
        ];

    public async Task InvokeAsync(HttpContext context, InMemoryETagStore eTagStore)
    {
        //1.如果当前请求方法是 POST、PUT、PATCH 或 DELETE，就跳过 ETag 逻辑
        if (CanSkipETag(context))
        {
            await next(context);
            return;
        }
        //2.获取当前请求的 URI，用作标识资源的 key，稍后要用来生成和比对 ETag。
        string resourceUri = context.Request.Path.Value!;
        //3.从请求头中读取客户端带来的 If-None-Match ETag，用于判断资源是否修改过。去掉引号是为了统一格式。
        string? ifNoneMatch = context.Request.Headers.IfNoneMatch.FirstOrDefault()?.Replace("\"", "");
        
        //①如果请求方法是 PUT 或 PATCH，就从请求头中读取 If-Match ETag，用于乐观锁校验
        string? ifMatch = context.Request.Headers.IfMatch.FirstOrDefault()?.Replace("\"", "");

        if (ConcurrencyCheckMethods.Contains(context.Request.Method) && !string.IsNullOrEmpty(ifMatch))
        {
            string currentETag = eTagStore.GetETag(resourceUri); //获取当前资源的 ETag
            //如果当前资源的 ETag 不为空，并且和请求头中的 If-Match ETag 不一致，就返回 412 Precondition Failed
            if (!string.IsNullOrWhiteSpace(currentETag) && ifMatch != currentETag) 
            {
                context.Response.StatusCode = StatusCodes.Status412PreconditionFailed;
                context.Response.ContentLength = 0;
                return;
            }
        }

        //4.如果请求方法是 GET 或 HEAD，就从 ETag 存储中获取当前资源的 ETag
        Stream originalStream = context.Response.Body; //获取原始响应流
        using var memoryStream = new MemoryStream(); //创建一个内存流，用于缓存响应内容
        context.Response.Body = memoryStream; //将响应流写入内存流，以便后续读取响应内容

        //5.执行请求管道中的下一个中间件（或控制器），并把响应写入 memoryStream 中g
        await next(context); 

        //6. 如果响应状态码是 200 OK，并且响应内容类型是 JSON，就计算 ETag
        if (IsETaggableResponse(context))
        {
            memoryStream.Position = 0; //将内存流位置重置到开头
            byte[] responseBody = await GetResponseBody(memoryStream); //读取内存流中的响应内容
            string eTag = GenerateETag(responseBody); //计算 ETag

            eTagStore.SetETag(resourceUri, eTag); //将 ETag 存储到 ETag 存储中
            context.Response.Headers.ETag = $"\"{eTag}\""; //将 ETag 添加到响应头中
            context.Response.Body = originalStream; //将响应流恢复为原始响应流

            //9. 如果 ETag 存储中已经有当前资源的 ETag，并且和计算出来的 ETag 一致，就返回 304 Not Modified
            if (context.Request.Method == HttpMethods.Get && ifNoneMatch == eTag) 
            {
                context.Response.StatusCode = StatusCodes.Status304NotModified; //返回 304 Not Modified
                context.Response.ContentLength = 0;//返回空响应体
                return;
            }
        }
        //如果内容有更新，复制缓冲的响应内容到原始响应流中，让客户端收到响应。
        memoryStream.Position = 0; //将内存流位置重置到开头
        await memoryStream.CopyToAsync(originalStream);//将内存流中的内容复制到原始响应流中
    }

    //判断当前响应是否适合使用 ETag 进行缓存处理
    private static bool IsETaggableResponse(HttpContext context)
    {
        return context.Response.StatusCode == StatusCodes.Status200OK &&
            (context.Response.Headers.ContentType
                .FirstOrDefault()?
                .Contains("json", StringComparison.OrdinalIgnoreCase) ?? false);
    }
    //读取 MemoryStream 中的响应内容，并以 byte[] 的形式返回
    private static async Task<byte[]> GetResponseBody(MemoryStream memoryStream)
    {
        using var reader = new StreamReader(memoryStream, leaveOpen: true);
        memoryStream.Position = 0;

        string content = await reader.ReadToEndAsync();

        return Encoding.UTF8.GetBytes(content);
    }
    //根据响应内容生成 ETag 值
    private static string GenerateETag(byte[] content)
    {
        byte[] hash = SHA512.HashData(content);
        return Convert.ToBase64String(hash);
    }

    //判断当前请求方法是否可以跳过 ETag 逻辑
    private static bool CanSkipETag(HttpContext context)
    {
        return context.Request.Method == HttpMethods.Post ||
            context.Request.Method == HttpMethods.Put ||
            context.Request.Method == HttpMethods.Patch ||
            context.Request.Method == HttpMethods.Delete;
    }
}
