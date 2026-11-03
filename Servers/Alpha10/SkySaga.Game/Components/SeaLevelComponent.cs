namespace SkySaga.Game.Components;

public class SeaLevelComponent : Component
{
    public int SeaLevel { get; set => SetIfChanged(ref field, value); }

    public int SeaFloorLevel { get; set => SetIfChanged(ref field, value); }

    public bool ShowSea { get; set => SetIfChanged(ref field, value); } = true;

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        switch (parameterName.ToLowerInvariant())
        {
            case "sealevel":
                bitStream.WriteFullWidthUInt32((uint)SeaLevel);
                return true;

            case "seafloorlevel":
                bitStream.WriteFullWidthUInt32((uint)SeaFloorLevel);
                return true;

            case "showsea":
                bitStream.Write(ShowSea);
                return true;
        }

        return false;
    }
}
