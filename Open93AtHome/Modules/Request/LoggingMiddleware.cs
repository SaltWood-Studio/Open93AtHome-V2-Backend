using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Open93AtHome.Modules.Request;
public class LoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly object _lock;

    public LoggingMiddleware(RequestDelegate next)
    {
        _next = next;
        this._lock = new object();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 调用下一个中间件
        await _next(context);

        // 请求完成后记录访问日志
        LogAccess(context);
    }

    public void LogAccess(HttpContext context)
    {
        lock (_lock)
        {
            context.Request.Headers.TryGetValue("user-agent", out StringValues value);
            Console.WriteLine($"{context.Request.Method} {context.Request.Path.Value} {context.Request.Protocol} <{context.Response.StatusCode}> - [{context.Connection.RemoteIpAddress}] {value.FirstOrDefault()}");
        }
    }
}
