namespace HoldMyBeer.Interaction
{
    /// <summary>Which hand holds an item. Serialised as a byte over the network.</summary>
    public enum HandSide : byte
    {
        None = 0,
        Left = 1,
        Right = 2
    }
}
