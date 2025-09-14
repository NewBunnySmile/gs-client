namespace GagSpeak.State.Models;

[Serializable]
public class EmoteTrigger : Trigger
{
    public override TriggerKind Type => TriggerKind.EmoteAction;

    // the emote ID to look for.
    public uint EmoteID { get; set; } = uint.MaxValue;

    // What state should the emote be in when triggering this?
    public TriggerDirection EmoteDirection { get; set; } = TriggerDirection.Self;

    // if the 'other' is player specific, define the player here.
    public string PlayerNameWorld { get; set; } = string.Empty;

    public EmoteTrigger()
    { }

    public EmoteTrigger(Trigger baseTrigger, bool keepId)
        : base(baseTrigger, keepId)
    { }

    public EmoteTrigger(EmoteTrigger other, bool keepId)
        : base(other, keepId)
    {
        EmoteID = other.EmoteID;
        EmoteDirection = other.EmoteDirection;
        PlayerNameWorld = other.PlayerNameWorld;
    }

    public override EmoteTrigger Clone(bool keepId) => new EmoteTrigger(this, keepId);

    public override void ApplyChanges(Trigger other)
    {
        base.ApplyChanges(other);
        if (other is not EmoteTrigger et)
            return;

        EmoteID = et.EmoteID;
        EmoteDirection = et.EmoteDirection;
        PlayerNameWorld = et.PlayerNameWorld;
    }
}
