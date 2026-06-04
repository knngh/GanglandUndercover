using System.Collections.Generic;
using System.Linq;
using GanglandUndercover.SocialDeduction;

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
        public SocialRole PlayerRole { get; private set; } = SocialRole.Undercover;
        public GamePhase Phase { get; private set; } = GamePhase.RoleSelect;
        public int Day { get; private set; } = 1;
        public int Evidence { get; private set; }
        public int PoliceHeat { get; private set; } = 2;
        public int ShipmentProgress { get; private set; }
        public int Cover { get; private set; } = 70;
        public int Suspicion { get; private set; } = 15;
        public int PublicTrust { get; private set; } = 6;
        public string Result { get; private set; } = string.Empty;

        // ── 双向渗透核心字段 ──
        /// <summary>卧底收集的证据量（警察阵营共享）</summary>
        public int UndercoverEvidence { get; private set; }
        /// <summary>线人情报量（黑帮阵营共享），达到阈值即识别出卧底</summary>
        public int MoleIntel { get; private set; }
        /// <summary>警察已知的黑帮成员 ID 列表</summary>
        public List<int> KnownGangToPolice { get; private set; } = new List<int>();
        /// <summary>黑帮已知的警察成员 ID 列表</summary>
        public List<int> KnownPoliceToGang { get; private set; } = new List<int>();
        /// <summary>卧底证据目标值</summary>
        public const int UndercoverEvidenceTarget = 10;
        /// <summary>线人情报目标值（识别卧底）</summary>
        public const int MoleIntelTarget = 10;
        /// <summary>最大天数（超过为僵局）</summary>
        public const int MaxDays = 10;

        public int GangControlledDistricts => districts.Count(district => district.Controller == Faction.Gang);
        public int PoliceControlledDistricts => districts.Count(district => district.Controller == Faction.Police);
        public int ContestedDistricts => districts.Count(district => district.Controller == Faction.Undercover);

        // --- Meeting / Elimination state ---
        public bool UndercoverEliminated { get; private set; }
        public bool PoliceEliminated { get; private set; }
        public bool GangEliminated { get; private set; }
        public bool MoleEliminated { get; private set; }
        public bool MeetingInProgress { get; set; }
        public Faction VotedOut { get; private set; } = (Faction)(-1); // sentinel: no vote

        public void EliminateFaction(Faction faction)
        {
            switch (faction)
            {
                case Faction.Gang:
                    GangEliminated = true;
                    break;
                case Faction.Undercover:
                    UndercoverEliminated = true;
                    break;
                case Faction.Police:
                    PoliceEliminated = true;
                    break;
            }

            VotedOut = faction;
            AddLog(faction + " 阵营在会议中被淘汰。");
        }

        /// <summary>
        /// 按 SocialRole 淘汰角色。
        /// </summary>
        public void EliminateRole(SocialRole role)
        {
            switch (role)
            {
                case SocialRole.Gang:
                    GangEliminated = true;
                    break;
                case SocialRole.Undercover:
                    UndercoverEliminated = true;
                    break;
                case SocialRole.Police:
                    PoliceEliminated = true;
                    break;
                case SocialRole.Mole:
                    MoleEliminated = true;
                    break;
            }

            AddLog(SocialKnowledge.DescribeRole(role) + " 在会议中被淘汰。");
        }

        public void ClearEliminations()
        {
            GangEliminated = false;
            UndercoverEliminated = false;
            PoliceEliminated = false;
            MoleEliminated = false;
            VotedOut = (Faction)(-1);
        }

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
            PlayerRole = SocialRole.Undercover;
            Day = 1;
            Evidence = 0;
            UndercoverEvidence = 0;
            MoleIntel = 0;
            KnownGangToPolice = new List<int>();
            KnownPoliceToGang = new List<int>();
            PoliceHeat = 2;
            ShipmentProgress = 0;
            Cover = 70;
            Suspicion = 15;
            PublicTrust = 6;
            Result = string.Empty;
            ClearEliminations();
            MeetingInProgress = false;
            AddLog("Port District initialized. Choose a side.");
        }

        public void SelectFaction(Faction faction)
        {
            PlayerFaction = faction;
            Phase = GamePhase.PlayerTurn;
            AddLog("Player joined: " + faction + ".");
        }

        /// <summary>
        /// 选择具体角色（双向渗透模型）。
        /// </summary>
        public void SelectRole(SocialRole role)
        {
            PlayerRole = role;
            PlayerFaction = SocialKnowledge.GetRealFaction(role);
            Phase = GamePhase.PlayerTurn;
            AddLog("玩家角色：" + SocialKnowledge.DescribeRole(role) + "。");
        }

        public void ToggleLanguage()
        {
            Language = Language == GameLanguage.Chinese ? GameLanguage.English : GameLanguage.Chinese;
        }

        public void AddEvidence(int amount)
        {
            Evidence = Clamp(Evidence + amount, 0, 10);
        }

        public void AddUndercoverEvidence(int amount)
        {
            UndercoverEvidence = Clamp(UndercoverEvidence + amount, 0, UndercoverEvidenceTarget);
        }

        public void AddMoleIntel(int amount)
        {
            MoleIntel = Clamp(MoleIntel + amount, 0, MoleIntelTarget);
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
