using Open93AtHome.Modules.Avro;
using Open93AtHome.Modules.Database;
using Open93AtHome.Modules.Statistics;

namespace Open93AtHome.Modules.Storage;

public class ClusterStatisticsHelper
{
    private Dictionary<string, ClusterStatistics> statistics;
    private readonly IEnumerable<ClusterEntity> clusters;

    public Dictionary<string, ClusterStatistics> Statistics => this.statistics;

    public ClusterStatisticsHelper(IEnumerable<ClusterEntity> clusters)
    {
        this.statistics = new Dictionary<string, ClusterStatistics>();
        this.clusters = clusters;
    }

    public void Save()
    {
        foreach (var cluster in clusters)
        {
            using (var fos = new FileStream(Path.Combine("./statistics", cluster.ClusterId), FileMode.Create))
            {
                var encoder = new AvroEncoder();
                encoder.SetString(cluster.ClusterId);
                ClusterStatistics value = this.statistics.ContainsKey(cluster.ClusterId) ? this.statistics[cluster.ClusterId] : new ClusterStatistics();
                foreach (var traffic in value.GetRawTraffic())
                {
                    encoder.SetLong(traffic);
                }
                foreach (var hits in value.GetRawHits())
                {
                    encoder.SetLong(hits);
                }
                encoder.SetEnd();
                fos.Write(encoder.ByteStream.ToArray(), 0, (int)encoder.ByteStream.Length);
            }
        }
    }

    public void Load()
    {
        foreach (var cluster in clusters)
        {
            try
            {
                using (var fis = new FileStream(Path.Combine("./statistics", cluster.ClusterId), FileMode.Open))
                {
                    AvroDecoder decoder = new AvroDecoder(fis);
                    if (decoder.GetString() != cluster.ClusterId) throw new Exception();
                    ClusterStatistics value = new ClusterStatistics();
                    foreach(int x in Enumerable.Range(0, 24))
                    {
                        foreach (int y in Enumerable.Range(0, 31))
                        {
                            value.SetTraffic(x, y, decoder.GetLong());
                        }
                    }
                    foreach(int x in Enumerable.Range(0, 24))
                    {
                        foreach (int y in Enumerable.Range(0, 31))
                        {
                            value.SetHits(x, y, decoder.GetLong());
                        }
                    }
                    if (!decoder.GetEnd()) throw new Exception();
                }
            }
            catch (Exception)
            {
                statistics[cluster.ClusterId] = new ClusterStatistics();
            }
        }
    }
}
