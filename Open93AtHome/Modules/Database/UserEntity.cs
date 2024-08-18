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

        public UserEntity(string userName, string userEmail, string password)
        {
            if (Utils.IsEmailReachable(userEmail)) throw new InvalidOperationException("邮箱地址不可达，原因是没有查询到MX记录。");
            UserName = userName;
            UserEmail = userEmail;
            Password = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLower();
        }

        public void SetPassword(string password) => Password = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLower();

        [AutoIncrement, Column("id")]
        public int Id { get; set; }

        [Indexed]
        [Column("username")]
        public string UserName { get; set; } = string.Empty;

        [Indexed]
        [Column("email")]
        public string UserEmail { get; set; } = string.Empty;

        [Indexed]
        [Column("password")]
        public string Password { get; set; } = string.Empty;

        public bool CheckPassword(string password) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLower() == password.ToLower();
    }
}
