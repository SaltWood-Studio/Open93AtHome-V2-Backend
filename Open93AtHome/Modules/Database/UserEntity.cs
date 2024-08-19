using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Open93AtHome.Modules.Database
{
    public class UserEntity
    {
        public UserEntity() { }

        public UserEntity(long id, string username, byte[] photo)
        {
        }

        [Column("id")]
        [PrimaryKey, Indexed]
        public long Id { get; set; }

        [Column("username")]
        public string UserName { get; set; } = string.Empty;

        [Column("photo")]
        public string Photo { get; set; } = string.Empty;
    }
}
