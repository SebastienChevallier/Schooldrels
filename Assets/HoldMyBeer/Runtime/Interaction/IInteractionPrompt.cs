namespace HoldMyBeer.Interaction
{
    /// <summary>
    /// What the local player could do right now, published for the HUD to draw.
    ///
    /// Exists so the UI never has to know about players, rules or Netcode: the
    /// interactor publishes a label, the crosshair reads it. Written by the local
    /// interactor only — it describes this machine's crosshair, not shared state.
    /// </summary>
    public interface IInteractionPrompt
    {
        /// <summary>The action under the crosshair, or null when there is none.</summary>
        string Label { get; }

        /// <summary>0 to 1 while an action is being held, 0 otherwise.</summary>
        float Charge { get; }

        bool IsCharging { get; }

        void Publish(string label, float charge, bool isCharging);
    }
}
