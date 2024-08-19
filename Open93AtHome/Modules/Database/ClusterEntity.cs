using Newtonsoft.Json;
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
        [Indexed, PrimaryKey]
        [Column("cluster_id")]
        [JsonProperty("clusterId")]
        public string ClusterId { get; set; } = string.Empty;

        [Column("cluster_secret")]
        [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public string ClusterSecret { get; set; } = string.Empty;

        [Column("endpoint")]
        [JsonProperty("endpoint")]
        public string Endpoint { get; set; } = string.Empty;

        [Column("port")]
        [JsonProperty("port")]
        public ushort Port { get; set; } = 80;

        [Column("owner")]
        [JsonProperty("owner")]
        public long Owner { get; set; } = 80;

        [Column("down_reason")]
        public string DownReason { get; set; } = string.Empty;

        [Column("cluster_name")]
        [JsonProperty("clusterName")]
        public string ClusterName { get; set; } = string.Empty;

        [Column("bandwidth")]
        public int Bandwidth { get; set; } = 30;

        [Ignore]
        [JsonProperty("measureBandwidth")]
        public int MeasureBandwidth { get; set; } = -1;

        [Column("traffic")]
        public long Traffic { get; set; } = 0;

        [Ignore]
        [JsonProperty("pendingTraffic")]
        public int PendingTraffic { get; set; } = 0;

        [Column("hits")]
        public long Hits { get; set; } = 0;

        [Ignore]
        [JsonProperty("pendingHits")]
        public int PendingHits { get; set; } = 0;

        [Ignore]
        [JsonProperty("isOnline")]
        public bool IsOnline { get; set; } = false;

        [Ignore]
        [JsonProperty("isBanned")]
        public bool IsBanned { get; set; } = false;

        [Ignore]
        [JsonProperty("ownerName")]
        public string OwnerName => BackendServer.DatabaseHandler.GetEntity<UserEntity>(this.Owner)?.UserName ?? string.Empty;

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
            return $"<{this.GetType().FullName} instance id={this.ClusterId} secret={this.ClusterSecret} code={this.GetHashCode()}>";
        }
    }
}
