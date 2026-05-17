namespace VoxScope.Effects;

internal sealed class HighBandEnergyReference
{
    private float _powerEnvelope;

    public float PowerEnvelope => Volatile.Read(ref _powerEnvelope);

    public void Update(float powerEnvelope)
    {
        Volatile.Write(ref _powerEnvelope, powerEnvelope);
    }
}
