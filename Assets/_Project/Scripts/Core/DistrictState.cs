using System;

namespace GanglandUndercover.Core
{
    [Serializable]
    public sealed class DistrictState
    {
        public DistrictState(DistrictType type, string displayName, int gangInfluence, int policePresence, int civilianTrust)
        {
            Type = type;
            DisplayName = displayName;
            GangInfluence = gangInfluence;
            PolicePresence = policePresence;
            CivilianTrust = civilianTrust;
        }

        public DistrictType Type { get; }
        public string DisplayName { get; }
        public int GangInfluence { get; private set; }
        public int PolicePresence { get; private set; }
        public int CivilianTrust { get; private set; }
        public bool HasWitness { get; private set; }
        public bool IsLockedDown { get; private set; }

        public Faction Controller
        {
            get
            {
                if (GangInfluence - PolicePresence >= 2)
                {
                    return Faction.Gang;
                }

                if (PolicePresence - GangInfluence >= 2)
                {
                    return Faction.Police;
                }

                return Faction.Undercover;
            }
        }

        public void AddGangInfluence(int amount)
        {
            GangInfluence = Clamp(GangInfluence + amount, 0, 10);
        }

        public void AddPolicePresence(int amount)
        {
            PolicePresence = Clamp(PolicePresence + amount, 0, 10);
        }

        public void AddCivilianTrust(int amount)
        {
            CivilianTrust = Clamp(CivilianTrust + amount, 0, 10);
        }

        public void SetWitness(bool hasWitness)
        {
            HasWitness = hasWitness;
        }

        public void SetLockdown(bool isLockedDown)
        {
            IsLockedDown = isLockedDown;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }
    }
}
