namespace SkySaga.Game;

public static class CustomisationBlock
{
    private const int AlphaBits = 231;
    private const int AlphaBytes = (AlphaBits + 7) / 8;

    private const uint MaterialEscape = 3;
    private const uint AttachmentEscape = 10;
    private const uint CustomisationEscape = 9;
    private const uint GearEscape = 11;
    private const uint GearMaterialEscape = 4;

    private static int Width(uint max) => (int)(32 - Util.NumBitsRequiredUInt32(max));

    private static int IndexWidth(string table) => Width(GeoIndex.Count(table) - 1);

    private sealed class Bits
    {
        private readonly List<bool> _bits = [];

        public int Count => _bits.Count;

        public void Add(uint value, int width)
        {
            var full = width / 8;
            var rem = width % 8;

            for (var i = 0; i < full; i++)
            {
                var b = (value >> (8 * i)) & 0xFF;
                for (var j = 7; j >= 0; j--)
                    _bits.Add(((b >> j) & 1) != 0);
            }

            if (rem > 0)
            {
                var b = (value >> (8 * full)) & ((1u << rem) - 1);
                for (var j = rem - 1; j >= 0; j--)
                    _bits.Add(((b >> j) & 1) != 0);
            }
        }

        public void Add(bool bit) => _bits.Add(bit);

        public byte[] ToBytes()
        {
            var data = new byte[(_bits.Count + 7) / 8];
            var full = _bits.Count / 8 * 8;

            for (var i = 0; i < full; i++)
                if (_bits[i])
                    data[i / 8] |= (byte)(0x80 >> (i % 8));

            var tail = _bits.Count - full;

            for (var i = 0; i < tail; i++)
                if (_bits[full + i])
                    data[full / 8] |= (byte)(1 << (tail - 1 - i));

            return data;
        }
    }

    private sealed class Reader(byte[] data, int bitCount)
    {
        private int _offset;

        public bool Read(int width, out uint value)
        {
            value = 0;

            if (_offset + width > bitCount)
                return false;

            var full = width / 8;
            var rem = width % 8;

            for (var i = 0; i < full; i++)
                value |= ReadRaw(8) << (8 * i);

            if (rem > 0)
                value |= ReadRaw(rem) << (8 * full);

            return true;
        }

        private uint ReadRaw(int count)
        {
            uint v = 0;

            for (var i = 0; i < count; i++)
                v = (v << 1) | (Bit(_offset++) ? 1u : 0u);

            return v;
        }

        private bool Bit(int index)
        {
            var fullBits = bitCount / 8 * 8;

            if (index < fullBits)
                return (data[index / 8] & (0x80 >> (index % 8))) != 0;

            var tail = bitCount - fullBits;

            return (data[fullBits / 8] & (1 << (tail - 1 - (index - fullBits)))) != 0;
        }
    }

    private delegate bool Source(int width, out uint value);

    private static bool Copy(Source read, Bits output)
    {
        bool Field(int width, out uint value)
        {
            if (!read(width, out value))
                return false;

            output.Add(value, width);
            return true;
        }

        bool Count(uint escape, out uint count)
        {
            if (!Field(Width(escape), out count))
                return false;

            if (count != escape)
                return true;

            if (!Field(1, out var more))
                return false;

            return more == 0;
        }

        var tribe = IndexWidth("Tribes");
        var material = IndexWidth("Materials");
        var attachment = IndexWidth("CharacterAttachments");
        var customisation = IndexWidth("MaterialCustomisations");
        var backstory = IndexWidth("Backstories");
        var item = Width(ItemTable.Count - 1);

        if (!Field(Width(2), out _) || !Field(tribe, out _))
            return false;

        if (!Count(MaterialEscape, out var materials))
            return false;

        for (var i = 0; i < materials; i++)
            if (!Field(material, out _))
                return false;

        if (!Count(AttachmentEscape, out var attachments))
            return false;

        for (var i = 0; i < attachments; i++)
            if (!Field(attachment, out _) || !Field(material, out _))
                return false;

        if (!Count(CustomisationEscape, out var customisations))
            return false;

        for (var i = 0; i < customisations; i++)
            if (!Field(customisation, out _))
                return false;

        if (!Count(GearEscape, out var gear))
            return false;

        for (var i = 0; i < gear; i++)
        {
            if (!Field(item, out _) || !Count(GearMaterialEscape, out var amounts))
                return false;

            for (var j = 0; j < amounts; j++)
            {
                if (!Field(1, out var present))
                    return false;

                if (present != 0 && !Field(32, out _))
                    return false;
            }
        }

        return Field(backstory, out _) && Field(1, out _) && Field(1, out _);
    }

    public static int BitLength(byte[] data)
    {
        for (var bits = data.Length * 8 - 7; bits <= data.Length * 8; bits++)
        {
            var reader = new Reader(data, bits);
            var output = new Bits();

            if (Copy(reader.Read, output) && output.Count == bits)
                return bits;
        }

        return 0;
    }

    public static bool TryRead(BitStream bitStream, out byte[] data)
    {
        var output = new Bits();

        bool Read(int width, out uint value)
        {
            value = 0;
            var buffer = new byte[4];

            if (!bitStream.ReadBits(buffer, (uint)width, true))
                return false;

            value = BitConverter.ToUInt32(buffer);
            return true;
        }

        data = Copy(Read, output) ? output.ToBytes() : [];

        return data.Length > 0;
    }

    public static byte[] Upgrade(byte[] data)
    {
        if (data.Length != AlphaBytes)
            return data;

        var reader = new Reader(data, AlphaBits);
        var output = new Bits();

        bool Copy(int width)
        {
            if (!reader.Read(width, out var value))
                return false;

            output.Add(value, width);
            return true;
        }

        var tribe = IndexWidth("Tribes");
        var material = IndexWidth("Materials");
        var attachment = IndexWidth("CharacterAttachments");
        var customisation = IndexWidth("MaterialCustomisations");
        var backstory = IndexWidth("Backstories");

        if (!Copy(Width(2)) || !Copy(tribe) || !Copy(Width(MaterialEscape)) || !Copy(1))
            return data;

        for (var i = 0; i < MaterialEscape; i++)
            if (!Copy(material))
                return data;

        if (!reader.Read(Width(5), out var attachments) || !reader.Read(1, out _))
            return data;

        output.Add(attachments, Width(AttachmentEscape));

        for (var i = 0; i < attachments; i++)
            if (!Copy(attachment) || !Copy(material))
                return data;

        if (!Copy(Width(CustomisationEscape)) || !Copy(1))
            return data;

        for (var i = 0; i < CustomisationEscape; i++)
            if (!Copy(customisation))
                return data;

        output.Add(0, Width(GearEscape));

        if (!Copy(backstory))
            return data;

        output.Add(false);
        output.Add(false);

        return output.ToBytes();
    }
}
