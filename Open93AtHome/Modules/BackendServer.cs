using Open93AtHome.Modules.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using static System.Net.Mime.MediaTypeNames;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Open93AtHome.Modules.Request;
using Newtonsoft.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Diagnostics;
using Open93AtHome.Modules.Avro;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Open93AtHome.Modules.Statistics;
using Open93AtHome.Modules.Storage;
using System.Text.Json;
using System.Net.Http.Json;
using System.Web;

namespace Open93AtHome.Modules
{
    public class BackendServer
    {
        protected DatabaseHandler _db;
        protected SocketIOClient.SocketIO _io;
        protected WebApplication _application;
        protected List<ClusterEntity> clusters;
        private MultiKeyDictionary<string, string, FileEntity> files;
        private Task? fileUpdateTask;
        private byte[] avroBytes = Array.Empty<byte>();
        private Config config;
        protected ClusterStatisticsHelper statistics;
        protected DateTime startTime;
        private long lastHeartbeat;
        private Task? proxyHeartbeatTask;
        private HttpClient client;

        private IEnumerable<Token> Tokens => _db.GetEntities<Token>();
        private IEnumerable<ClusterEntity> OnlineClusters => this.clusters.Where(c => c.IsOnline);
        private IEnumerable<UserEntity> Users => this._db.GetEntities<UserEntity>();

        protected MultiKeyDictionary<string, string, FileEntity> Files
        {
            get => files;
            set
            {
                this.files = value;
                this.avroBytes = this.GenerateAvroFileList();
            }
        }

        protected byte[] GenerateAvroFileList()
        {
            lock (this.avroBytes)
            {
                AvroEncoder encoder = new AvroEncoder();
                encoder.SetElements(this.Files.Values.Count());
                foreach (var file in this.Files.Values)
                {
                    encoder.SetString(file.Path);
                    encoder.SetString(file.Hash);
                    encoder.SetLong(file.Size);
                    encoder.SetLong(file.LastModified);
                }
                encoder.SetEnd();
                return encoder.ByteStream.ToArray();
            }
        }

        public BackendServer(Config config)
        {
            this.client = new HttpClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "93@Home-Center/2.0.0");

            this.config = config;
            this._db = new DatabaseHandler();
            this.startTime = DateTime.Now;

            this._db.CreateTable<Token>();
            this._db.CreateTable<ClusterEntity>();
            this._db.CreateTable<FileEntity>();
            this._db.CreateTable<UserEntity>();

            this.clusters = this._db.GetEntities<ClusterEntity>().ToList();
            this.files = new MultiKeyDictionary<string, string, FileEntity>();
            this.statistics = new ClusterStatisticsHelper(this.clusters);
            this.statistics.Load();

            foreach (var file in this._db.GetEntities<FileEntity>())
            {
                this.files.Add(file.Hash, file.Path, file);
            }

            this.avroBytes = this.GenerateAvroFileList();

            this._io = null!;
            this.ReconnectToProxy().Wait();

            X509Certificate2? cert = LoadAndConvertCert(config.CertificateFile, config.CertificateKeyFile);
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseKestrel(options =>
            {
                options.ListenAnyIP(config.HttpsPort, configure =>
                {
                    configure.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
                    if (cert != null && config.HttpsPort != ushort.MinValue) configure.UseHttps(cert);
                });
            });

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll",
                    builder => builder.AllowAnyOrigin()
                                      .AllowAnyHeader()
                                      .AllowAnyMethod());
            });

            this._application = builder.Build();

            this._application.UseRouting();
            this._application.UseMiddleware<LoggingMiddleware>();
            // _application.UseCors("AllowAll");

            _application.MapPost("/93AtHome/add_cluster", async (context) =>
            {
                context.Response.Headers.Append("Content-Type", "application/json");
                if (!await Utils.CheckPermission(context, true, Tokens)) return;
                var dict = await context.GetRequestDictionary() ?? new Dictionary<object, object>();
                string name = (string)dict["name"];
                int bandwidth = (int)dict["bandwidth"];
                ClusterEntity entity = ClusterEntity.CreateClusterEntity();
                entity.ClusterName = name;
                entity.Bandwidth = bandwidth;
                this._db.AddEntity(entity);
                this.clusters.Add(entity);
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(entity));
            });

            _application.MapGet("/93AtHome/list_cluster", async (context) =>
            {
                context.Response.Headers.Append("Content-Type", "application/json");
                if (!await Utils.CheckPermission(context, false, Tokens)) return;
                ClusterEntity entity = ClusterEntity.CreateClusterEntity();
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(this.clusters));
            });

            _application.MapGet("/93AtHome/list_file", async (context) =>
            {
                context.Response.Headers.Append("Content-Type", "application/json");
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(this.files));
            });

            _application.MapPost("/93AtHome/remove_cluster", async (context) =>
            {
                context.Response.Headers.Append("Content-Type", "application/json");
                if (!await Utils.CheckPermission(context, true, Tokens)) return;
                var dict = await context.GetRequestDictionary() ?? new Dictionary<object, object>();
                string clusterId = (string)dict["clusterId"];
                ClusterEntity searchParam = new ClusterEntity();
                searchParam.ClusterId = clusterId;
                int count = this._db.RemoveEntity<ClusterEntity>(clusterId);
                this.clusters.RemoveAll(c => c.ClusterId == clusterId);
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new
                {
                    isRemoved = count != 0,
                    elementsRemoved = count,
                    clusterId
                }));
            });

            _application.MapGet("/openbmclapi-agent/challenge", async context =>
            {
                context.Response.Headers.Append("Content-Type", "application/json");
                context.Request.Query.TryGetValue("clusterId", out StringValues values);
                string clusterId = values.First() ?? string.Empty;
                if (this.clusters.Any(c => c.ClusterId == clusterId))
                {
                    context.Response.StatusCode = 200;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        challenge = JwtHelper.Instance.GenerateToken("93@Home-Center-Server", "cluster-challenge", [new Claim("clusterId", clusterId)], 60 * 5)
                    });
                }
                else
                {
                    context.Response.StatusCode = 404;
                }
            });

            _application.MapPost("/openbmclapi-agent/token", async context =>
            {
                context.Response.Headers.Append("Content-Type", "application/json");
                IDictionary<object, object>? kvp = await context.GetRequestDictionary();
                string clusterId = (string)kvp!["clusterId"];
                string signature = (string)kvp!["signature"];
                string challenge = (string)kvp!["challenge"];
                if (this.clusters.Any(c => c.ClusterId == clusterId))
                {
                    var claims = JwtHelper.Instance.ValidateToken(challenge, "93@Home-Center-Server", "cluster-challenge")?.Claims;
                    if (claims != null && claims.Any(claim => claim.Type == "clusterId" &&
                        claim.Value == clusterId))
                    {
                        context.Response.StatusCode = 200;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            token = JwtHelper.Instance.GenerateToken("93@Home-Center-Server", "cluster", [new Claim("clusterId", clusterId)], 60 * 60 * 24)
                        });
                    }
                    else
                    {
                        context.Response.StatusCode = 403;
                    }
                }
                else
                {
                    context.Response.StatusCode = 404;
                }
            });

            _application.MapGet("/openbmclapi-agent/configuration", async context =>
            {
                context.Response.Headers.Append("Content-Type", "application/json");
                if (!Utils.CheckClusterRequest(context)) return;
                context.Response.StatusCode = 200;
                await context.Response.WriteAsJsonAsync(new
                {
                    sync = new
                    {
                        source = "center",
                        concurrency = 12
                    }
                });
            });

            _application.MapGet("/openbmclapi/download/{hash}", async (HttpContext context, string hash) =>
            {
                if (!Utils.CheckClusterRequest(context)) return;
                string? path = this.Files.GetByKey1(hash)?.Path;
                if (path != null)
                {
                    string realPath = Path.Combine(config.FileDirectory, path.Substring(7));
                    await context.Response.SendFileAsync(realPath);
                }
            });

            _application.MapGet("/files/{*file}", async (HttpContext context, string file) =>
            {
                if (file.StartsWith("..") || file.Contains("/../") || file.EndsWith("/.."))
                {
                    context.Response.StatusCode = 418;
                    await context.Response.WriteAsync("想啥呢孩子.png");
                    return;
                }
                file = file.StartsWith('/') ? file : ('/' + file);
                file = "/files" + file;
                if (this.OnlineClusters.Count() == 0)
                {
                    string realPath = Path.Combine(config.FileDirectory, file.Substring(7));
                    await context.Response.SendFileAsync(realPath);
                    return;
                }
                else
                {
                    FileEntity? f = this.Files.GetByKey2(file);
                    if (f != null)
                    {
                        context.Response.StatusCode = 302;
                        context.Response.Headers.Location = Utils.GetDownloadUrl(this.OnlineClusters.Random(), f);
                        return;
                    }
                }
            });

            _application.MapGet("/93AtHome/update_files", async context =>
            {
                if (!await Utils.CheckPermission(context, false, Tokens)) return;
                if (this.fileUpdateTask?.Status <= TaskStatus.WaitingForChildrenToComplete)
                {
                    context.Response.StatusCode = 409;
                    return;
                }
                this.fileUpdateTask = Task.Run(() =>
                {
                    Process process = new Process()
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            WorkingDirectory = config.FileDirectory,
                            FileName = "git",
                            Arguments = "pull"
                        }
                    };
                    process.Start();
                    process.WaitForExit(60000);
                    if (process.ExitCode != 0) return;
                    var files = Utils.ScanFiles(config.FileDirectory).Select(f => $"/files{f}");
                    HashSet<FileEntity> oldFileList = this.Files.Values.ToHashSet();
                    HashSet<FileEntity> updateFileList = new HashSet<FileEntity>();
                    HashSet<FileEntity> newFiles = new HashSet<FileEntity>();
                    foreach (string file in files)
                    {
                        string realPath = Path.Combine(config.FileDirectory, file.Substring(7));
                        FileInfo info = new FileInfo(realPath);
                        using Stream stream = File.OpenRead(realPath);
                        FileEntity entity = new FileEntity(stream, info, file);
                        updateFileList.Add(entity);
                    }
                    foreach (var file in updateFileList)
                    {
                        if (!oldFileList.Any(f => f == file))
                        {
                            newFiles.Add(file);
                        }
                    }
                    this._db.RemoveAll<FileEntity>();
                    this.files = new MultiKeyDictionary<string, string, FileEntity>();
                    foreach (var file in updateFileList)
                    {
                        this._db.AddEntity(file);
                        this.files.Add(file.Hash, file.Path, file);
                    }
                    this.avroBytes = this.GenerateAvroFileList();
                });
                context.Response.StatusCode = 204;
            });

            _application.MapGet("/93AtHome/rank", async context =>
            {
                context.Response.Headers.Append("Content-Type", "application/json");
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(this.clusters));
            });

            _application.MapGet("/93AtHome/random", context =>
            {
                context.Response.StatusCode = 302;
                context.Response.Headers.Location = Utils.GetDownloadUrl(this.OnlineClusters.Random(), this.Files.Values.Random());
                return Task.CompletedTask;
            });

            _application.MapGet("/93AtHome/onlines", async context =>
            {
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync(OnlineClusters.Count().ToString());
            });

            _application.MapGet("/93AtHome/dashboard/oauth_id", async context =>
            {
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync(config.GitHubOAuthClientId);
            });

            _application.MapGet("/93AtHome/dashboard/user/oauth", async context =>
            {
                context.Response.Headers.Append("Content-Type", "application/json");
                try
                {
                    string code = context.Request.Query["code"].FirstOrDefault() ?? string.Empty;

                    HttpClient http = this.client;
                    HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
                    {
                        Content = JsonContent.Create(new
                        {
                            code,
                            client_id = config.GitHubOAuthClientId,
                            client_secret = config.GitHubOAuthClientSecret
                        })
                    };
                    requestMessage.Headers.Add("Accept", "application/json");
                    var response = await http.SendAsync(requestMessage);
                    response.EnsureSuccessStatusCode();

                    IDictionary<string, string> token = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>() ?? new();
                    string accessToken = token["access_token"];

                    requestMessage = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
                    requestMessage.Headers.Add("Authorization", $"token {accessToken}");
                    requestMessage.Headers.Add("Accept", "application/json");
                    response = await http.SendAsync(requestMessage);
                    GitHubUser user = JsonConvert.DeserializeObject<GitHubUser>(await response.Content.ReadAsStringAsync()) ?? new GitHubUser();
                    if (_db.GetEntity<UserEntity>(user.Id) != null) _db.Update(user);
                    else _db.AddEntity<UserEntity>(user);


                    context.Response.Cookies.Append("token",
                        JwtHelper.Instance.GenerateToken("93@Home-Center-Server", "user",
                        [
                            new Claim("github_id", user.Id.ToString())
                        ], 60 * 60 * 24), new CookieOptions
                        {
                            Expires = DateTime.UtcNow.AddDays(1),
                            Secure = true
                        });

                    await context.Response.WriteAsJsonAsync(new
                    {
                        avatar_url = user.AvatarUrl,
                        username = user.Login,
                        id = user.Id
                    });
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = $"{ex.GetType().FullName}: {ex.Message}"
                    });
                }
            });

            _application.MapGet("/93AtHome/dashboard/user/profile", async context =>
            {
                UserEntity? current = await Utils.CheckCookies(context, Users);
                if (current == null) return;
                context.Response.Headers.Append("Content-Type", "application/json");
                await context.Response.WriteAsync(JsonConvert.SerializeObject(current));
            });

            _application.MapPost("/93AtHome/dashboard/user/bindCluster", async context =>
            {
                UserEntity? user = await Utils.CheckCookies(context, Users);
                if (user == null) return;
                context.Response.Headers.Append("Content-Type", "application/json");

                var body = await context.GetRequestDictionary();

                ClusterEntity? cluster = clusters.Where(c =>
                c.ClusterId == (string)body!["clusterId"] &&
                c.ClusterSecret == (string)body!["clusterSecret"]).FirstOrDefault();
                if (cluster == null)
                {
                    context.Response.StatusCode = 404;
                    return;
                }
                if (cluster.Owner != -1)
                {
                    context.Response.StatusCode = 409;
                    return;
                }
                cluster.Owner = user.Id;
                _db.Update(cluster);
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(cluster));
            });

            _application.MapPost("/93AtHome/dashboard/user/unbindCluster", async context =>
            {
                UserEntity? user = await Utils.CheckCookies(context, Users);
                if (user == null) return;
                context.Response.Headers.Append("Content-Type", "application/json");

                var body = await context.GetRequestDictionary();

                ClusterEntity? cluster = clusters.Where(c =>
                c.ClusterId == (string)body!["clusterId"]).FirstOrDefault();
                if (cluster == null)
                {
                    context.Response.StatusCode = 404;
                    return;
                }
                if (cluster.Owner == -1 || cluster.Owner != user.Id)
                {
                    context.Response.StatusCode = 403;
                    return;
                }
                cluster.Owner = -1;
                _db.Update(cluster);
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(cluster));
            });

            _application.MapPost("/93AtHome/user/{id}", async (HttpContext context, int id) =>
            {
                UserEntity? user = _db.GetEntities<UserEntity>().Where(c => c.Id == id).FirstOrDefault();
                context.Response.Headers.Append("Content-Type", "application/json");
                if (user == null)
                {
                    context.Response.StatusCode = 404;
                    return;
                }
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync(JsonConvert.SerializeObject((GitHubUser)user));
            });

            _application.MapPost("/93AtHome/user/clusters", async (HttpContext context) =>
            {
                UserEntity? user = await Utils.CheckCookies(context, Users);
                if (user == null)
                {
                    context.Response.StatusCode = 403;
                    return;
                }
                context.Response.Headers.Append("Content-Type", "application/json");
                IEnumerable<ClusterEntity> clusters = _db.GetEntities<ClusterEntity>().Where(c => c.Owner == user.Id);
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(clusters));
            });

            _application.MapPost("/93AtHome/cluster/{id}", (HttpContext context, string id) =>
            {
                ClusterEntity? cluster = this.clusters.Where(c => c.ClusterId == id).FirstOrDefault();
                ClusterStatistics? s = this.statistics.Statistics.Where(s => s.Key == cluster?.ClusterId).FirstOrDefault().Value;
                if (!(cluster != null && s != null))
                {
                    context.Response.StatusCode = 404;
                    return;
                }
                context.Response.WriteAsync(JsonConvert.SerializeObject(new
                {
                    traffic_per_hour = s.GetRawTraffic(),
                    hits_per_hour = s.GetRawHits(),
                    cluster
                })).Wait();
            });

            _application.MapPost("/93AtHome/onlines", async context => await context.Response.WriteAsync(this.clusters.Where(c => c.IsOnline).Count().ToString()));
            _application.MapPost("/93AtHome/clusterStatus", async context =>
            {
                await context.Response.WriteAsJsonAsync(new
                {
                    version = config.Version,
                    uptime = (DateTime.Now - this.startTime).TotalSeconds
                });
            });
        }

        protected async Task ReconnectToProxy()
        {
            string handshakeSignature = Utils.RandomHexString(128);
            this._io = new SocketIOClient.SocketIO(config.SocketIOAddress);

            await this._io.ConnectAsync();

            this._io.On("proxy-keep-alive", ack =>
            {
                this.lastHeartbeat = DateTimeOffset.Now.ToUnixTimeSeconds();
                if (ack.GetValue<JsonElement>(0).GetString() != "I am the proxy server.") Console.WriteLine("Incorrect proxy heartbeat message.");
            });

            using (Stream file = File.Create(config.SocketIOHandshakeFile))
            {
                file.Write(Encoding.UTF8.GetBytes(handshakeSignature));
                file.Close();
            }

            await this._io.EmitAsync("center-inject", (ack) =>
            {
                this.lastHeartbeat = DateTimeOffset.Now.ToUnixTimeSeconds();
                this.proxyHeartbeatTask = Task.Run(() =>
                {
                    while (true)
                    {
                        if (DateTimeOffset.Now.ToUnixTimeSeconds() - lastHeartbeat > 60 * 5)
                        {
                            this.proxyHeartbeatTask = null;
                            try
                            {
                                this._io.DisconnectAsync().Wait();
                            }
                            catch { }
                            this.ReconnectToProxy().Wait();
                            return;
                        }
                        Thread.Sleep(1000 * 60);
                    }
                });
            },
            new
            {
                handshake = handshakeSignature
            });
        }

        protected X509Certificate2? LoadAndConvertCert(string? certPath, string? keyPath)
        {
            if (!File.Exists(certPath) || !File.Exists(keyPath))
            {
                return null;
            }
            X509Certificate2 cert = X509Certificate2.CreateFromPemFile(certPath, keyPath);
            byte[] pfxCert = cert.Export(X509ContentType.Pfx);
            using (var file = File.Create("./cert.pfx"))
            {
                file.Write(pfxCert);
            }
            cert = new X509Certificate2(pfxCert);
            return cert;
        }

        public void Start() => _application.Run();

        public static (long startByte, long endByte) GetRange(string rangeHeader, long fileSize)
        {
            if (rangeHeader.Length <= 6) return (0, fileSize);
            var ranges = rangeHeader[6..].Split("-");
            try
            {
                if (ranges[1].Length > 0)
                {
                    return (long.Parse(ranges[0]), long.Parse(ranges[1]));
                }
            }
            catch (Exception)
            {
                return (long.Parse(ranges[0]), fileSize - 1);
            }

            return (long.Parse(ranges[0]), fileSize - 1);
        }
    }
}
