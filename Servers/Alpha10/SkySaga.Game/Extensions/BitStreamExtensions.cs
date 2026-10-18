using System.Text;

namespace SkySaga.Game.Extensions;

public static class BitStreamExtensions
{
    private const int EscapeBias = byte.MaxValue + 1 - (byte)DefaultMessageIDTypes.ID_USER_PACKET_ENUM;

    #region Read

    public static int ReadMessageId(this BitStream bitStream) => bitStream.ReadMessageId(out _);

    public static int ReadMessageId(this BitStream bitStream, out bool wasExtended)
    {
        bitStream.Read(out byte messageIdPart);

        if (messageIdPart != byte.MaxValue)
        {
            wasExtended = false;
            return messageIdPart;
        }

        wasExtended = true;

        bitStream.Read(out byte escapedPart);

        var ordinal = escapedPart + (EscapeBias - 1);

        return ordinal + (byte)DefaultMessageIDTypes.ID_USER_PACKET_ENUM;
    }

    public static string ReadString(this BitStream bitStream)
    {
        var hasData = bitStream.ReadBit();

        if (!hasData)
            return string.Empty;

        var largeLength = bitStream.ReadBit();

        if (largeLength)
            throw new NotImplementedException();

        var lengthData = new byte[1];

        if (!bitStream.ReadBits(lengthData, 8u))
            return string.Empty;

        var length = lengthData[0];

        var stringData = new byte[length];

        if (!bitStream.ReadBits(stringData, length * 8u))
            return string.Empty;

        return Encoding.UTF8.GetString(stringData);
    }

    public static bool ReadByte(this BitStream bitStream, byte maxValue, out byte result)
    {
        var buffer = new byte[1];
        var bitsToRead = 8 - Util.NumBitsRequiredByte(maxValue);

        if (!bitStream.ReadBits(buffer, bitsToRead, alignBitsToRight: true))
        {
            result = 0;
            return false;
        }

        result = buffer[0];
        return true;
    }

    public static bool ReadInt32(this BitStream bitStream, uint maxValue, out int result)
    {
        var buffer = new byte[4];
        var bitsToRead = 32 - Util.NumBitsRequiredUInt32(maxValue);

        if (!bitStream.ReadBits(buffer, bitsToRead, alignBitsToRight: true))
        {
            result = 0;
            return false;
        }

        result = BitConverter.ToInt32(buffer, 0);
        return true;
    }

    public static bool ReadFullWidthUInt32(this BitStream bitStream, out uint result)
    {
        var buffer = new byte[4];

        if (!bitStream.ReadBits(buffer, 32, alignBitsToRight: true))
        {
            result = 0;
            return false;
        }

        result = (uint)(buffer[0] << 24 | buffer[1] << 16 | buffer[2] << 8 | buffer[3]);
        return true;
    }

    public static bool ReadClientUuid(this BitStream bitStream, out Guid? result)
    {
        result = null;

        if (!bitStream.ReadInt32(1, out var present))
            return false;

        if (present == 0)
            return true;

        var hex = new System.Text.StringBuilder(32);

        for (var i = 0; i < 4; i++)
        {
            if (!bitStream.ReadFullWidthUInt32(out var word))
                return false;

            var chunk = word.ToString("x8").ToCharArray();

            Array.Reverse(chunk);
            hex.Append(chunk);
        }

        result = Guid.ParseExact(hex.ToString(), "N");
        return true;
    }

    public static bool ReadEscapedCount(this BitStream bitStream, uint escape, out uint count)
    {
        count = 0;

        if (!bitStream.ReadInt32(escape, out var value))
            return false;

        count = (uint)value;

        if (count == escape)
        {
            if (!bitStream.ReadInt32(1, out var present))
                return false;

            if (present != 0 && !bitStream.ReadFullWidthUInt32(out count))
                return false;
        }

        return true;
    }

    #endregion

    #region Write

    public static void WritePacketId(this BitStream bitStream, PacketId packetId)
        => bitStream.WritePacketId((ushort)packetId);

    public static void WritePacketId(this BitStream bitStream, ushort ordinal)
    {
        var direct = ordinal + (byte)DefaultMessageIDTypes.ID_USER_PACKET_ENUM;

        if (direct < byte.MaxValue)
        {
            bitStream.Write((byte)direct);
            return;
        }

        var escapedPart = ordinal - (EscapeBias - 1);

        if (escapedPart is < 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(ordinal), ordinal,
                "Packet ordinal does not fit the 0xFF + one-byte escape form.");

        bitStream.Write(byte.MaxValue);
        bitStream.Write((byte)escapedPart);
    }

    public static void WriteRangedIndex(this BitStream bitStream, uint value, uint tableCount)
    {
        var bitsToWrite = 32 - Util.NumBitsRequiredUInt32(tableCount - 1);

        bitStream.WriteBits(BitConverter.GetBytes(value), bitsToWrite, true);
    }

    public static void WriteFullWidthUInt32(this BitStream bitStream, uint value)
    {
        var bytes = BitConverter.GetBytes(value);

        Array.Reverse(bytes);

        bitStream.WriteBits(bytes, 32, true);
    }

    public static void WriteEscapedCount(this BitStream bitStream, uint count, uint escape)
    {
        bitStream.WriteRanged(Math.Min(count, escape), escape);

        if (count >= escape)
        {
            bitStream.Write1();
            bitStream.WriteFullWidthUInt32(count);
        }
    }

    public static void WriteRanged(this BitStream bitStream, uint value, uint max)
    {
        var bitsToWrite = 32 - Util.NumBitsRequiredUInt32(max);

        bitStream.WriteBits(BitConverter.GetBytes(value), bitsToWrite, true);
    }

    public static void WriteUInt64(this BitStream bitStream, ulong value)
    {
        bitStream.WriteBits(BitConverter.GetBytes(value), sizeof(ulong) * 8, true);
    }

    public static void WriteString(this BitStream bitStream, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            bitStream.Write0();

            return;
        }

        bitStream.Write1();

        var bytes = Encoding.UTF8.GetBytes(value);

        var length = bytes.Length;

        if (length < 255)
        {
            bitStream.Write0();
            bitStream.Write((byte)length);
        }
        else
        {
            bitStream.Write1();
            bitStream.Write(length);
        }

        bitStream.WriteBits(bytes, (uint)length * 8u, true);
    }

    public static void WriteOptional<T>(this BitStream bitStream, T? value, Action<T> write) where T : struct
    {
        bitStream.Write(value.HasValue);

        if (value.HasValue)
            write(value.Value);
    }

    public static void WriteOptional<T>(this BitStream bitStream, T? value, Action<T> write) where T : class
    {
        bitStream.Write(value is not null);

        if (value is not null)
            write(value);
    }

    public static void WriteClientUuid(this BitStream bitStream, Guid? value)
    {
        if (value is null)
        {
            bitStream.Write0();

            return;
        }

        bitStream.Write1();

        var hex = value.Value.ToString("N");

        for (var i = 0; i < 4; i++)
        {
            var chunk = hex.Substring(i * 8, 8).ToCharArray();

            Array.Reverse(chunk);

            var word = Convert.ToUInt32(new string(chunk), 16);

            bitStream.WriteBits(BitConverter.GetBytes(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(word)), 32, true);
        }
    }

    public static void WriteGuid(this BitStream bitStream, Guid? value)
    {
        bitStream.WriteOptional(value, (value) =>
        {
            var guidBytes = value.ToByteArray();

            bitStream.Write(guidBytes, (uint)guidBytes.Length);
        });
    }

    public static void WriteParameterFlags(this BitStream bitStream, int count, params int[] flags)
    {
        var data = new byte[16];

        var bitArray = new BitArray(count);

        foreach (var flag in flags)
            bitArray.Set(flag, true);

        bitArray.CopyTo(data, 0);

        for (int i = 0; i < count / 32; i++)
            Array.Reverse(data, i * 4, 4);

        bitStream.WriteBits(data, (uint)count);
    }

public static void WriteCompactSmall(this BitStream bitStream, uint value, uint threshold)
    {
        var bitsToWrite = 32 - Util.NumBitsRequiredUInt32(threshold);

        bitStream.WriteBits(BitConverter.GetBytes(value), bitsToWrite, true);

        bitStream.Write0();
    }

    public static void WriteCrc32List(this BitStream bitStream, IReadOnlyList<uint> values)
    {
        bitStream.WriteCompactSmall((uint)values.Count, 16);

        foreach (var value in values)
            bitStream.WriteBits(BitConverter.GetBytes(value), 32, true);
    }

    #endregion
}