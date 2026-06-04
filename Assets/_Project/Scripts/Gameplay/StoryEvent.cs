using GanglandUndercover.Core;

namespace GanglandUndercover.Gameplay
{
    public sealed class StoryEvent
    {
        public StoryEvent(string id, string title, string description)
        {
            Id = id;
            Title = title;
            Description = description;
        }

        public string Id { get; }
        public string Title { get; }
        public string Description { get; }

        public string FormatLog()
        {
            return "Event - " + Title + ": " + Description;
        }
    }
}
