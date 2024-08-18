using Open93AtHome.Modules.Hash;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Open93AtHome.Modules.Database
{
    public class ClusterEntity
    {
        [AutoIncrement]
        [Newtonsoft.Json.JsonIgnore, JsonIgnore]
        [Column("id")]
        public int Id { get; set; }

        [Indexed, PrimaryKey]
        [Column("cluster_id")]
        public string ClusterId { get; set; } = string.Empty;

        [Column("cluster_secret")]
        [Newtonsoft.Json.JsonIgnore, JsonIgnore]
        public string ClusterSecret { get; set; } = string.Empty;

        [Column("endpoint")]
        public string Endpoint { get; set; } = string.Empty;

        [Column("port")]
        public ushort Port { get; set; } = 80;

        [Column("down_reason")]
        public string DownReason { get; set; } = string.Empty;

        [Column("cluster_name")]
        public string ClusterName { get; set; } = string.Empty;

        [Column("bandwidth")]
        public int Bandwidth { get; set; } = 30;

        [Ignore]
        public int MeasureBandwidth { get; set; } = -1;

        [Ignore]
        public bool IsOnline { get; set; } = false;

        public static ClusterEntity CreateClusterEntity()
        {
            ClusterEntity clusterEntity = new ClusterEntity();
            clusterEntity.ClusterId = Utils.GenerateHexString(24);
            clusterEntity.ClusterSecret = Utils.GenerateHexString(32);
            return clusterEntity;
        }

        public override int GetHashCode()
        {
            return CRC32.ComputeChecksum(Encoding.UTF8.GetBytes(ClusterId)).ToInteger();
        }

        public override string ToString()
        {
            return $"<{this.GetType().FullName} instance index={this.Id} id={this.ClusterId} secret={this.ClusterSecret} code={this.GetHashCode()}>";
        }
    }
}
