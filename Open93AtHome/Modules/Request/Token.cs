using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Open93AtHome.Modules.Request
{
    public class Token
    {
        const int MaxTokenLength = 512;

        [Indexed]
        [Column("bytes")]
        public byte[] Bytes { get; set; } = new byte[MaxTokenLength / 8];

        [Column("hasAllPermission")]
        public bool HasAllPermission { get; set; } = false;

        public bool CheckPermission(string token, bool allPermission) => 
            this.Bytes.EqualsAll(SHA256.HashData(Encoding.UTF8.GetBytes(token))) &&
                (this.HasAllPermission || (!this.HasAllPermission && !allPermission));

        public static Token Create()
        {
            Token token = new Token();
            token.Bytes = new byte[MaxTokenLength / 8];
            Random random = new Random();
            random.NextBytes(token.Bytes);
            return token;
        }
    }
}
