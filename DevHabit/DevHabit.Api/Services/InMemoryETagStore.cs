using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace DevHabit.Api.Services;

public sealed class InMemoryETagStore
{
    /// ETag 存储，使用 ConcurrentDictionary 线程安全地存储 ETag
    private static readonly ConcurrentDictionary<string, string> ETags = new();

    //从 ETags 字典中获取该 URI 对应的ETag值，果不存在，GetOrAdd 会为该 URI 返回一个默认值，
    public string GetETag(string resourceUri)
    {
        return ETags.GetOrAdd(resourceUri, _ => string.Empty);
    }
    //使用 AddOrUpdate 方法将 ETag 更新到字典中
    public void SetETag(string resourceUri, string etag)
    {
        ETags.AddOrUpdate(resourceUri, etag, (_, _) => etag);
    }
    //(乐观锁用)SetETag方法的重载，接受资源 URI 和资源对象 resource，调用 GenerateETag 方法来生成资源对象的 ETag，并将其存储
    public void SetETag(string resourceUri, object resource)
    {
        ETags.AddOrUpdate(resourceUri, GenerateETag(resource), (_, _) => GenerateETag(resource));
    }

    public void RemoveETag(string resourceUri)
    {
        ETags.TryRemove(resourceUri, out _);
    }

    //生成资源对象的 ETag
    private static string GenerateETag(object resource)
    {
        //将资源对象序列化为 JSON 字符串，并转换为字节数组
        byte[] content = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(resource)); 
        
        byte[] hash = SHA512.HashData(content); // 计算字节数组的 SHA-512 哈希值
        return Convert.ToBase64String(hash); // 将哈希值转换为 Base64 字符串
    }
}
