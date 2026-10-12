using System.Text;

namespace SkySaga.Game.Extensions;

public static class BitStreamExtensions
{
    /// <summary>
    /// Ordinals whose direct byte encoding (ordinal + ID_USER_PACKET_ENUM) would reach 0xFF have
    /// to use the escape form, because 0xFF is the escape marker itself. That threshold is
    /// ordinal 121 (121 + 134 == 255), so the escape byte carries (ordinal - 121) and covers
    /// ordinals 121..376 - comfortably past the highest ordinal in <see cref="PacketId"/> (340).
    /// </summary>
    private const int EscapeBias = byte.MaxValue + 1 - (byte)DefaultMessageIDTypes.ID_USER_PACKET_ENUM; // 122

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

















        // The escape form is 0xFF followed by ONE byte holding (ordinal - EscapeBias + 1),
        // which is exactly what WritePacketId emits. This used to read 16 bits here, which is
        // where "UNHANDLED user packet raw=0x93 messageId=281 (TakeMailAttachment)" came from:
        // the client's 'ff 93' is ordinal 0x93 + 121 = 268 (RequestStartAnimationSequence), but
        // the 16-bit read swallowed the following payload byte and decoded 147 instead.
        // EntityAdd (ordinal 153) going out as 'ff 20' - 153 - 121 = 0x20 - and being accepted
        // by the real client is the proof that the one-byte escape is the on-wire format.
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

    /// <summary>
    /// A full-width u32 as the client writes it: the four bytes in network (big-endian) order.
    /// Counterpart of WriteFullWidthUInt32.
    /// </summary>
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

    #endregion

    #region Write







    public static void WritePacketId(this BitStream bitStream, PacketId packetId)
        => bitStream.WritePacketId((ushort)packetId);

    /// <summary>
    /// Single writer for every packet id. Ordinals below the escape threshold go out as one byte
    /// (ordinal + ID_USER_PACKET_ENUM); everything from 121 up goes out as 0xFF followed by one
    /// byte holding (ordinal - 121).
    ///
    /// This replaces the old pair of overloads. The byte overload produced the correct bytes but
    /// only by relying on unsigned wraparound, and the separate WritePacketIdExtended wrote a
    /// 16-bit index that nothing on the client reads - it was only reachable for ordinals >= 255,
    /// so every packet in the 255..340 range (the whole animation-sequence block) was malformed.
    /// </summary>
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

    /// <summary>
    /// A full-width 32-bit field. The client reads these through FUN_006FB780 and byte-swaps them
    /// in this build (DAT_01449B5E is zero), so they go out big-endian.
    /// </summary>
    public static void WriteFullWidthUInt32(this BitStream bitStream, uint value)
    {
        var bytes = BitConverter.GetBytes(value);

        Array.Reverse(bytes);

        bitStream.WriteBits(bytes, 32, true);
    }

    /// <summary>
    /// A list count as the client's list readers take it: ranged up to <paramref name="escape"/>;
    /// the value <paramref name="escape"/> itself is followed by a presence bit and the real count
    /// as a full-width u32 (e.g. FUN_00BC7550, FUN_00C426B0, FUN_00D18620).
    /// </summary>
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

    /// <summary>
    /// A uuid the way the client keeps it: one presence bit, then four 32-bit words, each written
    /// like every other 32-bit field (the client byte-reverses on read). Word i is the i-th group of
    /// 8 hex digits of the canonical string, read with its digits reversed: the client's formatter
    /// prints each word low nibble first. So "a7cfad0a-9634-..." goes out as words 0xa0dafc7a,
    /// 0x14456943, ... and the client prints (and compares with the web service's strings) exactly
    /// "a7cfad0a-9634-...".
    ///
    /// Measured 2026-10-12: Guid.ToByteArray() words (the old encoding) made ServerInfo's owner
    /// a7cfad0a-9634-4154-8b33-95425743f4ae arrive as 7afcdaa0-1445-6943-2459-33b8ea4f3475.
    /// </summary>
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