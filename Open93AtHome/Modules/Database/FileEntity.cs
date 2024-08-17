using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
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

    }
}
