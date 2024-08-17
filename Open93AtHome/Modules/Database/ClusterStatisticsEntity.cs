using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Open93AtHome.Modules.Database
{
    public class ClusterStatisticsEntity
    {
        [Column("cluster_id")]
        public string ClusterId { get; set; } = string.Empty;

        [PrimaryKey, Indexed]
        [Column("time")]
        public DateTime Time { get; set; } = DateTime.Today;

        [Column("hits")]
        public ulong Hits { get; set; }

        [Column("bytes")]
        public ulong Bytes { get; set; }
    }
}
