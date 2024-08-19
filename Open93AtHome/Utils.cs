using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.JsonWebTokens;
using Open93AtHome.Modules;
using Open93AtHome.Modules.Database;
using Open93AtHome.Modules.Request;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
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

        public static bool IsEmailReachable(string emailAddress)
        {
            try
            {
                // 解析电子邮件地址
                var email = new MailAddress(emailAddress);
                string domain = email.Host;

                // 查询 MX 记录
                var mxRecords = GetMxRecords(domain);

                // 如果 MX 记录不为空，则邮件地址可达
                return mxRecords.Length > 0;
            }
            catch (FormatException)
            {
                // 邮件地址格式不正确
                return false;
            }
            catch (Exception ex)
            {
                // 处理其他异常
                Console.WriteLine($"Error: {ex.Message}");
                return false;
            }
        }

        private static string[] GetMxRecords(string domain)
        {
            try
            {
                var mxRecords = new List<string>();

                // 查询 MX 记录
                var dns = new DnsEndPoint(domain, 0);
                var addresses = Dns.GetHostEntry(domain).AddressList;

                foreach (var address in addresses)
                {
                    mxRecords.Add(address.ToString());
                }

                return mxRecords.ToArray();
            }
            catch (Exception ex)
            {
                // 处理 DNS 查询异常
                Console.WriteLine($"DNS Error: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        public static async ValueTask<UserEntity?> CheckCookies(HttpContext context, IEnumerable<UserEntity> users)
        {
            string requestToken = context.Request.Cookies["token"] ?? "";
            string? userId = JwtHelper.Instance.ValidateToken(requestToken, "93@Home-Center-Server", "user")?
                .Claims.Where(claim => claim.Type == "github_id").FirstOrDefault()?.Value;
            int.TryParse(userId, out int id);
            UserEntity? user = users.Where(u => u.Id == id).FirstOrDefault();
            if (user == null)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("UNAUTHORIZED.");
            }
            return user;
        }

        public static int ToInteger(this uint value)
        {
            // 将 uint 值分成高位和低位
            int highPart = (int)(value >> 31); // 获取高位
            int lowPart = (int)(value & 0x7FFFFFFF); // 获取低位

            // 组合成一个 int 值
            return (highPart == 0 ? 1 : -1) * lowPart; // 将高位和低位组合
        }

        public static T Random<T>(this IEnumerable<T> values)
        {
            Random rnd = new Random();
            T[] elements = values.ToArray();
            return elements[rnd.Next(0, values.Count() - 1)];
        }

        public static string GenerateSign(string path, ClusterEntity cluster)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 5 * 60 * 1000;
            string e = Convert.ToString(timestamp, 36);
            byte[] data = Encoding.UTF8.GetBytes($"{cluster.ClusterSecret}{path}{e}");
            byte[] signBytes = SHA1.HashData(data);
            string sign = ToUrlSafeBase64String(signBytes);
            return $"s={sign}&e={e}";
        }

        public static string GetDownloadUrl(ClusterEntity cluster, FileEntity file)
        {
            UriBuilder uriBuilder = new UriBuilder();
            uriBuilder.Scheme = "http";
            uriBuilder.Host = cluster.Endpoint;
            uriBuilder.Port = cluster.Port;
            uriBuilder.Path = $"/download/{file.Hash}";
            uriBuilder.Query = $"?{GenerateSign(file.Hash, cluster)}";
            return uriBuilder.ToString();
        }

            private static string ToUrlSafeBase64String(byte[] data)
        {
            string base64 = Convert.ToBase64String(data);
            return base64.Replace('+', '-').Replace('/', '_').Replace("=", string.Empty);
        }

        public static string GenerateHexString(int length)
        {
            const string hex = "0123456789abcdef";
            Random random = new Random();

            if (length < 0)
            {
                throw new ArgumentException("Length must be a positive integer");
            }

            StringBuilder hexString = new StringBuilder(length);

            for (int i = 0; i < length; i++)
            {
                int index = random.Next(hex.Length);
                hexString.Append(hex[index]);
            }

            return hexString.ToString();
        }

        public static ulong ByteArrayToUInt64(byte[] blob)
        {
            ulong result = 0;

            for (int j = 0; j < 8; j++)
            {
                result *= 256;
                result += blob[j];
            }

            return result;
        }

        /// <summary>
        /// 扫描指定目录，返回该目录下所有文件的路径集合
        /// </summary>
        /// <param name="directoryPath">要扫描的目录路径</param>
        /// <returns>包含所有文件相对路径的 HashSet 集合</returns>
        public static HashSet<string> ScanFiles(string directoryPath)
        {
            HashSet<string> filePaths = new HashSet<string>();
            DirectoryInfo directory = new DirectoryInfo(directoryPath);
            if (directory.Exists && directory.Attributes.HasFlag(FileAttributes.Directory))
            {
                ScanDirectory(directory, filePaths, directoryPath);
            }
            return filePaths;
        }

        /// <summary>
        /// 递归扫描目录及其子目录，收集所有文件的相对路径
        /// </summary>
        /// <param name="directory">当前扫描的目录</param>
        /// <param name="filePaths">存储文件路径的 HashSet 集合</param>
        /// <param name="rootPath">根目录路径，用于计算相对路径</param>
        private static void ScanDirectory(DirectoryInfo directory, HashSet<string> filePaths, string rootPath)
        {
            FileInfo[] files = directory.GetFiles();
            DirectoryInfo[] directories = directory.GetDirectories();

            foreach (FileInfo file in files)
            {
                // 忽略以点开头的文件
                if (file.Name.StartsWith("."))
                {
                    continue;
                }

                // 计算相对于根目录的路径
                string relativePath = file.FullName.Substring(rootPath.Length).Replace(Path.DirectorySeparatorChar, '/');
                if (!relativePath.StartsWith("/"))
                {
                    relativePath = "/" + relativePath;
                }

                filePaths.Add(relativePath);
            }

            // 递归扫描子目录
            foreach (DirectoryInfo subDirectory in directories)
            {
                if (!subDirectory.Name.StartsWith(".")) // 忽略以点开头的子目录
                {
                    ScanDirectory(subDirectory, filePaths, rootPath);
                }
            }
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
