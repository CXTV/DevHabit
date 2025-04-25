using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;

namespace DevHabit.Api.DTOs.Entries;

public sealed record EntryCursorDto(string Id, DateOnly Date)
{
    //将一个游标（ID 和时间）编码为字符串，前端分页请求时可用
    public static string Encode(string id, DateOnly date)
    {
        var cursor = new EntryCursorDto(id, date); // 创建一个游标对象
        string json = JsonSerializer.Serialize(cursor); // 序列化为 JSON 字符串
        return Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(json)); // 转成 Base64，避免 JSON 暴露或格式错误
    }

    public static EntryCursorDto Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }
        try
        {
            string json = Base64UrlEncoder.Decode(cursor); //解码 Base64 字符串
            return JsonSerializer.Deserialize<EntryCursorDto>(json); // 反序列化回游标对象
        }
        catch
        {
            return null;
        }
    }
}
