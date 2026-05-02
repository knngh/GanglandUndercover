using System.Collections.Generic;
using System.Linq;

namespace GanglandUndercover.Core
{
    public sealed class GameState
    {
        private readonly List<DistrictState> districts = new List<DistrictState>();
        private readonly List<string> log = new List<string>();

        public GameState()
        {
            Reset();
        }

        public IReadOnlyList<DistrictState> Districts => districts;
        public IReadOnlyList<string> Log => log;
        public GameLanguage Language { get; private set; } = GameLanguage.Chinese;
        public Faction PlayerFaction { get; private set; } = Faction.Undercover;
        public GamePhase Phase { get; private set; } = GamePhase.RoleSelect;
        public int Day { get; private set; } = 1;
        public int Evidence { get; private set; }
        public int PoliceHeat { get; private set; } = 2;
        public int ShipmentProgress { get; private set; }
        public int Cover { get; private set; } = 70;
        public int Suspicion { get; private set; } = 15;
        public int PublicTrust { get; private set; } = 6;
        public string Result { get; private set; } = string.Empty;

        public int GangControlledDistricts => districts.Count(district => district.Controller == Faction.Gang);
        public int PoliceControlledDistricts => districts.Count(district => district.Controller == Faction.Police);
        public int ContestedDistricts => districts.Count(district => district.Controller == Faction.Undercover);

        public DistrictState GetDistrict(DistrictType type)
        {
            return districts.First(district => district.Type == type);
        }

        public void Reset()
        {
            districts.Clear();
            districts.Add(new DistrictState(DistrictType.Dockyard, "Dockyard", 5, 3, 4));
            districts.Add(new DistrictState(DistrictType.WarehouseRow, "Warehouse Row", 6, 2, 3));
            districts.Add(new DistrictState(DistrictType.NightMarket, "Night Market", 4, 3, 7));
            districts.Add(new DistrictState(DistrictType.PolicePrecinct, "Police Precinct", 1, 7, 5));
            districts.Add(new DistrictState(DistrictType.Clinic, "Clinic", 2, 4, 8));
            districts.Add(new DistrictState(DistrictType.TenementBlock, "Tenement Block", 4, 2, 6));

            GetDistrict(DistrictType.TenementBlock).SetWitness(true);

            log.Clear();
            Language = GameLanguage.Chinese;
            Phase = GamePhase.RoleSelect;
            PlayerFaction = Faction.Undercover;
            Day = 1;
            Evidence = 0;
            PoliceHeat = 2;
            ShipmentProgress = 0;
            Cover = 70;
            Suspicion = 15;
            PublicTrust = 6;
            Result = string.Empty;
            AddLog("Port District initialized. Choose a side.");
        }

        public void SelectFaction(Faction faction)
        {
            PlayerFaction = faction;
            Phase = GamePhase.PlayerTurn;
            AddLog("Player joined: " + faction + ".");
        }

        public void ToggleLanguage()
        {
            Language = Language == GameLanguage.Chinese ? GameLanguage.English : GameLanguage.Chinese;
        }

        public void AddEvidence(int amount)
        {
            Evidence = Clamp(Evidence + amount, 0, 10);
        }

        public void AddPoliceHeat(int amount)
        {
            PoliceHeat = Clamp(PoliceHeat + amount, 0, 10);
        }

        public void AddShipmentProgress(int amount)
        {
            ShipmentProgress = Clamp(ShipmentProgress + amount, 0, 3);
        }

        public void AddCover(int amount)
        {
            Cover = Clamp(Cover + amount, 0, 100);
        }

        public void AddSuspicion(int amount)
        {
            Suspicion = Clamp(Suspicion + amount, 0, 100);
        }

        public void AddPublicTrust(int amount)
        {
            PublicTrust = Clamp(PublicTrust + amount, 0, 10);
        }

        public void AdvanceDay()
        {
            Day++;
        }

        public void SetPhase(GamePhase phase)
        {
            Phase = phase;
        }

        public void Finish(string result)
        {
            Result = result;
            Phase = GamePhase.GameOver;
            AddLog(result);
        }

        public void AddLog(string message)
        {
            log.Add("Day " + Day + ": " + message);

            while (log.Count > 12)
            {
                log.RemoveAt(0);
            }
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
