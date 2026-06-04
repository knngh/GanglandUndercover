using GanglandUndercover.Core;

namespace GanglandUndercover.Gameplay
{
    public sealed class PlayerAction
    {
        public PlayerAction(string id, string label, Faction faction, string description)
        {
            Id = id;
            Label = label;
            Faction = faction;
            Description = description;
        }

        public string Id { get; }
        public string Label { get; }
        public Faction Faction { get; }
        public string Description { get; }
    }
}
