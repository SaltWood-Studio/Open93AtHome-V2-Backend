using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.JsonWebTokens;
using Open93AtHome.Modules;
using Open93AtHome.Modules.Request;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Open93AtHome
{
    public static class Utils
    {
        public static string RandomHexString(int count)
        {
            StringBuilder sb = new StringBuilder();
            Random random = new Random();
            char[] chars = "0123456789abcdef".ToCharArray();
            foreach (var _ in Enumerable.Range(0, count))
            {
                sb.Append(random.GetItems(chars, chars.Length));
            }
            return sb.ToString();
        }

        public static void ForEach<T>(this IEnumerable<T> values, Action<T> action)
        {
            foreach (var value in values)
            {
                action.Invoke(value);
            }
        }

        public static async ValueTask<bool> CheckPermission(HttpContext context, bool needAllPermission, IEnumerable<Token> tokens)
        {
            context.Request.Query.TryGetValue("token", out StringValues values);
            string requestToken = values.First() ?? "";
            bool result = tokens.Any(f => f.CheckPermission(requestToken, needAllPermission));
            if (!result)
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("FORBIDDEN.");
            }
            return result;
        }

        public static bool CheckAuthorization(HttpContext context, bool needAllPermission, IEnumerable<Token> tokens)
        {
            string requestToken = context.Request.Headers.Authorization.FirstOrDefault()?.Split(' ').LastOrDefault() ?? string.Empty;
            return tokens.Any(f => f.CheckPermission(requestToken, needAllPermission));
        }

        public static bool CheckClusterRequest(HttpContext context)
        {
            string requestToken = context.Request.Headers.Authorization.FirstOrDefault()?.Split(' ').LastOrDefault() ?? string.Empty;
            return JwtHelper.Instance.ValidateToken(requestToken, "93@Home-Center-Server", "cluster") != null;
        }

        public static async Task<IDictionary<object, object>?> GetRequestDictionary(this HttpContext context)
        {
            try
            {
                return (await context.Request.ReadFormAsync()) as Dictionary<object, object>;
            }
            catch
            {
                try
                {
                    return await context.Request.ReadFromJsonAsync<Dictionary<object, object>>();
                }
                catch
                {
                    return null;
                }
            }
        }

        public unsafe static bool EqualsAll(this byte[] arr1, byte[]? arr2) //如果代码不是nullable就去掉"?"
        {
            if (Object.ReferenceEquals(arr1, arr2)) return true;

            int length = arr1.Length;
            if (arr2 == null || length != arr2.Length)
                return false;

            if (length < 4)
            {
                for (int i = 0; i < arr1.Length; i++)
                {
                    if (arr1[i] != arr2[i])
                        return false;
                }
                return true;
            }
            else
            {
                fixed (void* voidby1 = arr1)
                {
                    fixed (void* voidby2 = arr2)
                    {
                        const int cOneCompareSize = 8;

                        var blkCount = length / cOneCompareSize;
                        var less = length % cOneCompareSize;

                        byte* by1, by2;

                        long* lby1 = (long*)voidby1;
                        long* lby2 = (long*)voidby2;
                        while (blkCount > 0)
                        {
                            if (*lby1 != *lby2)
                                return false;
                            lby1++; lby2++;
                            blkCount--;
                        }

                        if (less >= 4) //此if和true的代码可以不要，性能差异不大
                        {
                            if (*((int*)lby1) != *((int*)lby2))
                                return false;

                            by1 = ((byte*)lby1 + 4);
                            by2 = ((byte*)lby2 + 4);

                            less = less - 4;
                        }
                        else
                        {
                            by1 = (byte*)lby1;
                            by2 = (byte*)lby2;
                        }

                        while (less-- > 0)
                        {
                            if (*by1 != *by2)
                                return false;
                            by1++; by2++;
                        }
                        return true;
                    }
                }
            }
        }

    }
}
