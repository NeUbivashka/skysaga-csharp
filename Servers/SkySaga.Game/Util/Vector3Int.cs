namespace SkySaga.Game;

public readonly struct Vector3Int(int x, int y, int z) : IEquatable<Vector3Int>
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Z { get; } = z;



    public int GetComponent(int index) => index switch
    {
        0 => X,
        1 => Y,
        2 => Z,
        _ => throw new ArgumentOutOfRangeException(nameof(index), "Index must be 0, 1, or 2")
    };



    public int GetLinearIndex(int gridSize = 32) => X + Y * gridSize + Z * gridSize * gridSize;



    public static Vector3Int FromLinearIndex(int index, int gridSize = 32)
    {
        int x = index % gridSize;
        int y = (index / gridSize) % gridSize;
        int z = index / (gridSize * gridSize);
        return new Vector3Int(x, y, z);
    }

    public override bool Equals(object? obj) => obj is Vector3Int other && Equals(other);

    public bool Equals(Vector3Int other) => X == other.X && Y == other.Y && Z == other.Z;

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public override string ToString() => $"({X}, {Y}, {Z})";

    public static bool operator ==(Vector3Int left, Vector3Int right) => left.Equals(right);
    public static bool operator !=(Vector3Int left, Vector3Int right) => !left.Equals(right);

    public static Vector3Int operator +(Vector3Int left, Vector3Int right)
        => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    public static Vector3Int operator -(Vector3Int left, Vector3Int right)
        => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    public static Vector3Int operator *(Vector3Int vec, int scalar)
        => new(vec.X * scalar, vec.Y * scalar, vec.Z * scalar);

    public static Vector3Int operator /(Vector3Int vec, int scalar)
        => new(vec.X / scalar, vec.Y / scalar, vec.Z / scalar);
}
