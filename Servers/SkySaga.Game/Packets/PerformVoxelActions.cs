namespace SkySaga.Game.Packets;

public static class PerformVoxelActions
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {

        if (!ReadLocation(bitStream, out var location)) return false;

        if (!ReadChunkCoordinate(bitStream, out var chunkCoordX)) return false;
        if (!ReadChunkCoordinate(bitStream, out var chunkCoordY)) return false;
        if (!ReadChunkCoordinate(bitStream, out var chunkCoordZ)) return false;

        if (!ReadVoxelCoordinate(bitStream, out var voxelCoordX)) return false;
        if (!ReadVoxelCoordinate(bitStream, out var voxelCoordY)) return false;
        if (!ReadVoxelCoordinate(bitStream, out var voxelCoordZ)) return false;

        if (!ReadSide(bitStream, out var side)) return false;

        if (!ReadPower(bitStream, out var power)) return false;

        if (!ReadPosition(bitStream, out var positionX)) return false;
        if (!ReadPosition(bitStream, out var positionY)) return false;
        if (!ReadPosition(bitStream, out var positionZ)) return false;

        if (!ReadDirection(bitStream, out var directionX)) return false;
        if (!ReadDirection(bitStream, out var directionY)) return false;
        if (!ReadDirection(bitStream, out var directionZ)) return false;

        Debug.WriteLine($"location: {location}, chunkCoord: ({chunkCoordX}, {chunkCoordY}, {chunkCoordZ}), voxelCoord: ({voxelCoordX}, {voxelCoordY}, {voxelCoordZ}), side: {side}, power: {power}, position: ({positionX}, {positionY}, {positionZ}), direction: ({directionX}, {directionY}, {directionZ})", nameof(PerformVoxelActions));

        var world = connection.WorldManager;
        var chunkPos = new Vector3Int(chunkCoordX, chunkCoordY, chunkCoordZ);
        var voxelPos = new Vector3Int(voxelCoordX, voxelCoordY, voxelCoordZ);

        int blockId = 0;
        if (location == ActionLocation.LeftHand && connection.LeftHandBlockId.HasValue)
        {
            voxelPos += new Vector3Int(directionX, directionY, directionZ);
            blockId = connection.LeftHandBlockId.Value;
        }
        if (location == ActionLocation.RightHand && connection.RightHandBlockId.HasValue)
        {
            voxelPos += new Vector3Int(directionX, directionY, directionZ);
            blockId = connection.RightHandBlockId.Value;
        }

        world.ChunkManager.SetVoxel(chunkPos, voxelPos, (byte)blockId);

        Debug.WriteLine($"Voxel at chunk {chunkPos}, voxel {voxelPos}, blockType {blockId}", nameof(PerformVoxelActions));

        return true;
    }

    private static bool ReadLocation(BitStream bitStream, out ActionLocation location)
    {
        if (!bitStream.ReadByte(8, out var tmpLocation))
        {
            location = 0;
            return false;
        }
        location = (ActionLocation)tmpLocation;
        return true;
    }

    private static bool ReadChunkCoordinate(BitStream bitStream, out int coord)
        => bitStream.ReadInt32(32, out coord);

    private static bool ReadVoxelCoordinate(BitStream bitStream, out int coord)
        => bitStream.ReadInt32(32, out coord);

    private static bool ReadSide(BitStream bitStream, out BlockSide side)
    {
        if (!bitStream.ReadByte(6, out var tmpSide))
        {
            side = 0;
            return false;
        }
        side = (BlockSide)tmpSide;
        return true;
    }

    private static bool ReadPower(BitStream bitStream, out float power)
    {
        if (!bitStream.ReadInt32(32, out var tmpPower))
        {
            power = 0;
            return false;
        }
        power = tmpPower / 32f;
        return true;
    }

    private static bool ReadPosition(BitStream bitStream, out float position)
    {
        if (!bitStream.ReadInt32(0x10000, out var tmpPosition))
        {
            position = 0;
            return false;
        }
        position = tmpPosition / 64f;
        return true;
    }

    private static bool ReadDirection(BitStream bitStream, out int direction)
    {
        if (!bitStream.ReadInt32(128, out var tmpDirection))
        {
            direction = 0;
            return false;
        }
        direction = tmpDirection / 64 - 1;
        return true;
    }
}
