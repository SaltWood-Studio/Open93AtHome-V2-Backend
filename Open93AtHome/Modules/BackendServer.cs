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

namespace Open93AtHome.Modules
{
    public class BackendServer
    {
        protected DatabaseHandler _db;
        protected SocketIOClient.SocketIO _io;
        protected WebApplication _application;
        protected List<ClusterEntity> clusters;
        protected MultiKeyDictionary<string, string, FileEntity> files;

        private IEnumerable<Token> tokens => _db.GetEntities<Token>();

        public BackendServer(Config config)
        {
            this._db = new DatabaseHandler();

            this._db.CreateTable<Token>();
            this._db.CreateTable<ClusterEntity>();
            this._db.CreateTable<FileEntity>();

            this.clusters = this._db.GetEntities<ClusterEntity>().ToList();
            this.files = new MultiKeyDictionary<string, string, FileEntity>();

            foreach (var file in this._db.GetEntities<FileEntity>())
            {
                this.files.Add(file.Hash, file.Path, file);
            }

            this._io = new SocketIOClient.SocketIO(config.SocketIOAddress);
            using (Stream file = File.Create(config.SocketIOHandshakeFile))
            {
                file.Write(Encoding.UTF8.GetBytes(Utils.RandomHexString(128)));
            }

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
            this._application = builder.Build();

            _application.MapPost("/93AtHome/add_cluster", async (context) =>
            {
                if (!await Utils.CheckPermission(context, true, tokens)) return;
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
                if (!await Utils.CheckPermission(context, true, tokens)) return;
                ClusterEntity entity = ClusterEntity.CreateClusterEntity();
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(this.clusters));
            });

            _application.MapPost("/93AtHome/remove_cluster", async (context) =>
            {
                if (!await Utils.CheckPermission(context, true, tokens)) return;
                var dict = await context.GetRequestDictionary() ?? new Dictionary<object, object>();
                string clusterId = (string)dict["clusterId"];
                ClusterEntity searchParam = new ClusterEntity();
                searchParam.ClusterId = clusterId;
                int count = this._db.RemoveEntity<ClusterEntity>(clusterId);
                this.clusters.RemoveAll(c =>  c.ClusterId == clusterId);
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
                context.Request.Query.TryGetValue("clusterId", out StringValues values);
                string clusterId = values.First() ?? string.Empty;
                if (this.clusters.Any(c => c.ClusterId == clusterId))
                {
                    context.Response.StatusCode = 200;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        challenge = JwtHelper.Instance.GenerateToken(clusterId, "93@Home-Center-Server", "cluster-challenge", 60 * 5)
                    });
                }
                else
                {
                    context.Response.StatusCode = 404;
                }
            });

            _application.MapPost("/openbmclapi-agent/token", async context =>
            {
                IDictionary<object, object>? kvp = await context.GetRequestDictionary();
                string clusterId = (string)kvp!["clusterId"];
                string signature = (string)kvp!["signature"];
                string challenge = (string)kvp!["challenge"];
                if (this.clusters.Any(c => c.ClusterId == clusterId))
                {
                    var claims = JwtHelper.Instance.ValidateToken(challenge, "93@Home-Center-Server", "cluster-challenge")?.Claims;
                    if (claims != null && claims.Any(claim => claim.Type == JwtRegisteredClaimNames.UniqueName &&
                        claim.Value == clusterId))
                    {
                        context.Response.StatusCode = 200;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            token = JwtHelper.Instance.GenerateToken(clusterId, "93@Home-Center-Server", "cluster-challenge", 60 * 60 * 24)
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
                string? path = this.files.GetByKey1(hash)?.Path;
                if (path != null)
                {
                    string realPath = Path.Combine(config.FileDirectory, '.' + path);
                    await context.Response.SendFileAsync(realPath);
                }
            });

            _application.MapGet("/files/{file}", async (HttpContent context, string file) =>
            {

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
            using (var file = File.Create("certificate/cert.pfx"))
            {
                file.Write(pfxCert);
            }
            cert = new X509Certificate2(pfxCert);
            return cert;
        }

        public static void LogAccess(HttpContext context)
        {
            context.Request.Headers.TryGetValue("user-agent", out StringValues value);
            Console.WriteLine($"{context.Request.Method} {context.Request.Path.Value} {context.Request.Protocol} <{context.Response.StatusCode}> - [{context.Connection.RemoteIpAddress}] {value.FirstOrDefault()}");
        }

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
