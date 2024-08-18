using System;
using System.IO;
using System.Text;

namespace Open93AtHome.Modules.Avro;

public class AvroEncoder
{
    public readonly MemoryStream ByteStream;

    public AvroEncoder()
    {
        ByteStream = new MemoryStream();
    }

    public static byte[] LongToByte(long value)
    {
        using var o = new MemoryStream();
        long data = (value << 1) ^ (value >> 63);
        while ((data & ~0x7F) != 0)
        {
            o.WriteByte((byte)((data & 0x7F) | 0x80));
            data >>= 7;
        }
        o.WriteByte((byte)data);
        return o.ToArray();
    }

    public void SetElements(long count) => SetLong(count);

    public void SetLong(long value) => ByteStream.Write(LongToByte(value));

    public void SetString(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        ByteStream.Write(LongToByte(bytes.Length));
        ByteStream.Write(bytes);
    }

    public void SetBytes(byte[] value)
    {
        ByteStream.Write(LongToByte(value.Length));
        ByteStream.Write(value);
    }

    public void SetEnd() => ByteStream.WriteByte(0x00);
}
