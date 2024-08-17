using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet;
using YamlDotNet.Serialization;

namespace Open93AtHome.Modules
{
    public class Config
    {
        [YamlMember(Description = "Socket.IO 服务器的地址，用于数据刷新。", Order = 0)]
        public string SocketIOAddress { get; set; } = "https://example.com:9300/";
        
        [YamlMember(Description = """
            与 Socket.IO 服务器握手用到的本地文件。
            将 Socket.IO 服务器与本程序的此设定项设定为同一个，这样就能验证并通过握手。
           """, Order = 0)]
        public string SocketIOHandshakeFile { get; set; } = "/path/to/handshake/file";

        [YamlMember(Description = "提供 HTTPS 服务的端口号。")]
        public ushort HttpsPort { get; set; } = ushort.MaxValue;

        [YamlMember(Description = "HTTPS 证书，cert")]
        public string CertificateFile { get; set; } = "/path/to/cert/cert.pem";

        [YamlMember(Description = "HTTPS 证书，key")]
        public string CertificateKeyFile { get; set; } = "/path/to/cert/key.pem";

        [YamlMember(Description = "文件路径")]
        public string FileDirectory { get; set; } = "/path/to/files";
    }
}
