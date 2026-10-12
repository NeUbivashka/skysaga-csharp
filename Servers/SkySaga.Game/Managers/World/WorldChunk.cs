namespace SkySaga.Game.Managers.World;

public class WorldChunk(Vector3Int position)
{
    private const int _chunkSize = 32;
    private const int _chunkVolume = _chunkSize * _chunkSize * _chunkSize;

    private readonly byte[] _voxels = new byte[_chunkVolume];
    private readonly byte[] _metadata = new byte[_chunkVolume];

    public Vector3Int Position { get; } = position;

    public byte GetVoxel(int x, int y, int z)
    {
        ValidateCoordinates(x, y, z);
        int index = CalculateIndex(x, y, z);
        return _voxels[index];
    }

    public byte GetVoxelMeta(int x, int y, int z)
    {
        ValidateCoordinates(x, y, z);
        return _metadata[CalculateIndex(x, y, z)];
    }

    public byte GetVoxel(Vector3Int localPos) => GetVoxel(localPos.X, localPos.Y, localPos.Z);

    public byte GetVoxelMeta(Vector3Int localPos) => GetVoxelMeta(localPos.X, localPos.Y, localPos.Z);

    public void SetVoxel(int x, int y, int z, byte blockType, byte metaValue = 0x00)
    {
        ValidateCoordinates(x, y, z);
        int index = CalculateIndex(x, y, z);
        _voxels[index] = blockType;
        _metadata[index] = metaValue;
    }

    public void SetVoxel(Vector3Int localPos, byte blockType, byte metaValue = 0x00) => SetVoxel(localPos.X, localPos.Y, localPos.Z, blockType, metaValue);

    public byte[] GetVoxelData() => (byte[])_voxels.Clone();

    public void SetVoxelData(byte[] data)
    {
        if (data.Length != _chunkVolume)
            throw new ArgumentException($"Voxel data must be exactly {_chunkVolume} bytes", nameof(data));

        Array.Copy(data, _voxels, _chunkVolume);
    }

    public byte[] GetMetadataForNetwork() => (byte[])_metadata.Clone();
    public byte[] GetRawMetadataForNetwork()
    {
        const int data2Size = 24576;
        byte[] data2 = new byte[data2Size];
        int bitPos = 0;

        for (int y = 0; y < _chunkSize; y++)
            for (int z = 0; z < _chunkSize; z++)
                for (int x = 0; x < _chunkSize; x++)
                {
                    byte sixBits = _metadata[CalculateIndex(x, y, z)];

                    for (int b = 0; b < 6; b++)
                    {
                        if ((sixBits & (1 << b)) != 0)
                        {
                            int byteIdx = bitPos >> 3;
                            int bitIdx = bitPos & 7;
                            data2[byteIdx] |= (byte)(1 << bitIdx);
                        }
                        bitPos++;
                    }
                }

        return data2;
    }

    public void Clear()
    {
        Array.Clear(_voxels, 0, _chunkVolume);
        Array.Clear(_metadata, 0, _chunkVolume);
    }

    public void Fill(byte blockType, byte metaValue = 0)
    {
        Array.Fill(_voxels, blockType);

        Array.Fill(_metadata, metaValue);
    }

    private static int CalculateIndex(int x, int y, int z) => x + z * _chunkSize + y * _chunkSize * _chunkSize;

    private static void ValidateCoordinates(int x, int y, int z)
    {
        if (x < 0 || x >= _chunkSize || y < 0 || y >= _chunkSize || z < 0 || z >= _chunkSize)
            throw new ArgumentOutOfRangeException($"Voxel coordinates must be 0-{_chunkSize - 1}");
    }
}
