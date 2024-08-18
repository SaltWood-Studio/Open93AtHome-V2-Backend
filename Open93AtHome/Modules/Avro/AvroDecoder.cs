using System;
using System.IO;
using System.Text;

namespace Open93AtHome.Modules.Avro;

public class AvroDecoder
{
    private readonly Stream byteStream;

    public AvroDecoder(Stream stream)
    {
        this.byteStream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    public long ByteToLong()
    {
        int b = byteStream.ReadByte();
        if (b == -1)
        {
            throw new EndOfStreamException();
        }
        long n = b & 0x7F;
        long shift = 7;
        while ((b & 0x80) != 0)
        {
            b = byteStream.ReadByte();
            if (b == -1)
            {
                throw new EndOfStreamException();
            }
            n |= (long)(b & 0x7F) << (int)shift;
            shift += 7;
        }
        return (n >> 1) ^ -(n & 1);
    }

    public string ByteToString() => Encoding.UTF8.GetString(GetBytes());

    public long GetElements() => GetLong();

    public long GetLong() => ByteToLong();

    public string GetString() => Encoding.UTF8.GetString(GetBytes());

    public byte[] GetBytes()
    {
        long length = ByteToLong();
        byte[] buffer = new byte[length];
        int bytesRead = byteStream.Read(buffer, 0, (int)length);
        if (bytesRead != length)
        {
            throw new EndOfStreamException();
        }
        return buffer;
    }

    public bool GetEnd() => byteStream.ReadByte() == 0;
}