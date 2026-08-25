using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.Online
{
    // B1: Data types moved from OnlineMatchController.cs

    public struct OnlinePlayerState
    {
        public OnlinePlayerState(ulong clientId, string displayName, Vector3 position, bool ready, bool alive, OnlineRole publicRole, OnlineProfession profession, int suspicion, bool isBot = false)
        {
            ClientId = clientId; DisplayName = displayName; Position = position;
            Input = Vector2.zero; Ready = ready; Alive = alive;
            PublicRole = publicRole; Profession = profession;
            KillCooldown = 0f; AbilityCooldown = 0f; VentCooldown = 0f;
            Suspicion = suspicion; IsBot = isBot; IsGhost = false;
            CharacterAnimator = null; SocialChar = null;
            Character2DDirectionIndicator = null; HasPendingAction = false;
        }
        public ulong ClientId;
        public string DisplayName;
        public Vector3 Position;
        public Vector2 Input;
        public bool Ready;
        public bool Alive;
        public bool IsGhost;
        public bool IsBot;
        public OnlineRole PublicRole;
        public OnlineProfession Profession;
        public float KillCooldown;
        public float AbilityCooldown;
        public float VentCooldown;
        public int Suspicion;
        public Animator CharacterAnimator;
        public SocialDeduction.SocialCharacter SocialChar;
        public GameObject Character2DDirectionIndicator;
        public bool HasPendingAction;
    }

    public struct OnlineTaskState
    {
        public OnlineTaskState(int id, string name, Vector3 position, int progress, int requiredProgress, bool completed, bool sabotaged)
        { Id = id; Name = name; Position = position; Progress = progress; RequiredProgress = requiredProgress; Completed = completed; Sabotaged = sabotaged; }
        public int Id;
        public string Name;
        public Vector3 Position;
        public int Progress;
        public int RequiredProgress;
        public bool Completed;
        public bool Sabotaged;
    }

    public struct OnlineBodyState
    {
        public OnlineBodyState(int id, ulong victimClientId, Vector3 position, bool reported)
        { Id = id; VictimClientId = victimClientId; Position = position; Reported = reported; }
        public int Id;
        public ulong VictimClientId;
        public Vector3 Position;
        public bool Reported;
    }

    public enum OnlineMatchPhase { Lobby, Opening, Action, Meeting, Voting, Result }
    public enum OnlineActionType { Interact, Report, Kill, Vote, SkipVote, Ability, Vent, Sabotage, Accuse }

    public struct MoleObjective
    {
        public int Kills;
        public int Sabotages;
        public bool SurvivedTilLate;
    }
}
