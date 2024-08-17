namespace Open93AtHome.Modules.Hash;

public static class MurmurHash
{
    public static uint Hash32(byte[] data, uint seed = 0)
    {
        const uint c1 = 0xcc9e2d51;
        const uint c2 = 0x1b873593;
        int length = data.Length;
        uint h1 = seed;

        // Process each 4-byte chunk
        for (int i = 0; i < length / 4; i++)
        {
            uint k1 = BitConverter.ToUInt32(data, i * 4);
            k1 *= c1;
            k1 = RotateLeft(k1, 15);
            k1 *= c2;
            h1 ^= k1;
            h1 = RotateLeft(h1, 13);
            h1 = h1 * 5 + 0xe6546b64;
        }

        // Process remaining bytes
        uint k2 = 0;
        int remaining = length % 4;
        if (remaining > 0)
        {
            for (int i = 0; i < remaining; i++)
            {
                k2 |= (uint)(data[length - remaining + i] << (i * 8));
            }

            k2 *= c1;
            k2 = RotateLeft(k2, 15);
            k2 *= c2;
            h1 ^= k2;
        }

        // Finalize hash
        h1 ^= (uint)length;
        h1 = FMix(h1);
        return h1;
    }

    private static uint RotateLeft(uint value, int count)
    {
        return (value << count) | (value >> (32 - count));
    }

    private static uint FMix(uint h)
    {
        h ^= h >> 16;
        h *= 0x85ebca6b;
        h ^= h >> 13;
        h *= 0xc2b2ae35;
        h ^= h >> 16;
        return h;
    }
}