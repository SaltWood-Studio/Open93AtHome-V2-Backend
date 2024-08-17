using Open93AtHome.Modules.Hash;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Open93AtHome.Modules.Database
{
    public class FileEntity
    {
        [Column("path")]
        public string Path { get; set; } = "/path/to/file";
        
        [Column("hash")]
        public string Hash { get; set; } = "0000000000000000000000000000000000000000";
        
        [Column("path")]
        public long Size { get; set; } = 0L;
        
        [Column("mtime")]
        public long LastModified { get; set; } = -1L;

        public FileEntity(Stream file, FileInfo info, string path)
        {
            using var _ = file;
            this.Path = path;
            this.Hash = Convert.ToHexString(SHA1.HashData(file)).ToLower();
            this.Size = file.Length;
            this.LastModified = new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeMilliseconds();
        }

        public static bool operator==(FileEntity? a, FileEntity? b)
        {
            return a?.Hash == b?.Hash;
        }

        public static bool operator!=(FileEntity? a, FileEntity? b)
        {
            return a?.Hash != b?.Hash;
        }

        public override bool Equals(object? obj)
        {
            FileEntity? entity = obj as FileEntity;
            if (entity != null)
            {
                return entity == this;
            }
            else return false;
        }

        public override int GetHashCode()
        {
            return CRC32.ComputeChecksum(Convert.FromHexString(this.Hash)).ToInteger();
        }
    }
}
