using HoldMyBeer.Interaction;

namespace HoldMyBeer.Gameplay.Interaction
{
    /// <summary>
    /// Plain holder for the crosshair state. Deliberately not a NetworkBehaviour and
    /// not replicated: what your crosshair says is a fact about your screen.
    /// </summary>
    public sealed class InteractionPrompt : IInteractionPrompt
    {
        public string Label { get; private set; }
        public float Charge { get; private set; }
        public bool IsCharging { get; private set; }

        public void Publish(string label, float charge, bool isCharging)
        {
            Label = label;
            Charge = charge;
            IsCharging = isCharging;
        }
    }
}
