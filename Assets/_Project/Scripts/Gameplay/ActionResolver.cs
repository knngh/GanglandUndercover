using System.Collections.Generic;
using GanglandUndercover.Core;

namespace GanglandUndercover.Gameplay
{
    public sealed class ActionResolver
    {
        private readonly List<PlayerAction> actions = new List<PlayerAction>
        {
            new PlayerAction("gang_expand", "Expand Turf", Faction.Gang, "Increase gang control in the selected district."),
            new PlayerAction("gang_ship", "Move Shipment", Faction.Gang, "Advance the major shipment, but increase police attention."),
            new PlayerAction("gang_silence", "Pressure Witness", Faction.Gang, "Remove a witness and damage public trust."),
            new PlayerAction("gang_bribe", "Bribe Checkpoint", Faction.Gang, "Clear a lockdown and weaken police presence."),
            new PlayerAction("police_investigate", "Investigate", Faction.Police, "Collect evidence from the selected district."),
            new PlayerAction("police_raid", "Raid", Faction.Police, "Reduce gang influence, but risk public trust."),
            new PlayerAction("police_protect", "Protect Witness", Faction.Police, "Secure a witness and improve public trust."),
            new PlayerAction("police_lockdown", "Set Checkpoint", Faction.Police, "Lock down a district to disrupt gang movement."),
            new PlayerAction("undercover_cover", "Maintain Cover", Faction.Undercover, "Act loyal to reduce suspicion and preserve cover."),
            new PlayerAction("undercover_intel", "Pass Intel", Faction.Undercover, "Gain evidence while risking suspicion."),
            new PlayerAction("undercover_sabotage", "Sabotage Shipment", Faction.Undercover, "Slow the gang and risk exposure."),
            new PlayerAction("undercover_dead_drop", "Dead Drop", Faction.Undercover, "Use a district handoff to build evidence without open police heat.")
        };

        public IEnumerable<PlayerAction> GetActionsFor(Faction faction)
        {
            foreach (PlayerAction action in actions)
            {
                if (action.Faction == faction)
                {
                    yield return action;
                }
            }
        }

        public void Resolve(GameState state, DistrictState district, PlayerAction action)
        {
            switch (action.Id)
            {
                case "gang_expand":
                    district.AddGangInfluence(2);
                    district.AddCivilianTrust(-1);
                    state.AddPoliceHeat(1);
                    state.AddSuspicion(-2);
                    state.AddLog("Gang expanded turf in " + district.DisplayName + ".");
                    break;
                case "gang_ship":
                    if (district.IsLockedDown)
                    {
                        state.AddPoliceHeat(1);
                        state.AddEvidence(1);
                        district.AddPolicePresence(1);
                        state.AddLog("Gang shipment stalled at a checkpoint in " + district.DisplayName + ".");
                        break;
                    }

                    state.AddShipmentProgress(1);
                    state.AddPoliceHeat(2);
                    district.AddGangInfluence(1);
                    district.AddPolicePresence(1);
                    state.AddLog("Gang moved cargo through " + district.DisplayName + ".");
                    break;
                case "gang_silence":
                    district.SetWitness(false);
                    district.AddGangInfluence(1);
                    district.AddCivilianTrust(-2);
                    state.AddPublicTrust(-1);
                    state.AddPoliceHeat(1);
                    state.AddLog("Gang pressured witnesses around " + district.DisplayName + ".");
                    break;
                case "gang_bribe":
                    if (district.IsLockedDown)
                    {
                        district.SetLockdown(false);
                        state.AddLog("Gang bribed a checkpoint crew in " + district.DisplayName + ".");
                    }
                    else
                    {
                        state.AddLog("Gang paid street lookouts around " + district.DisplayName + ".");
                    }

                    district.AddGangInfluence(1);
                    district.AddPolicePresence(-1);
                    district.AddCivilianTrust(-1);
                    state.AddPoliceHeat(1);
                    state.AddPublicTrust(-1);
                    break;
                case "police_investigate":
                    state.AddEvidence(district.HasWitness ? 2 : 1);
                    state.AddPoliceHeat(1);
                    district.AddPolicePresence(1);
                    state.AddLog("Police collected evidence in " + district.DisplayName + ".");
                    break;
                case "police_raid":
                    district.AddGangInfluence(-2);
                    district.AddPolicePresence(2);
                    state.AddPoliceHeat(2);
                    state.AddPublicTrust(-1);
                    state.AddLog("Police raided " + district.DisplayName + ".");
                    break;
                case "police_protect":
                    district.SetWitness(true);
                    district.AddPolicePresence(1);
                    district.AddCivilianTrust(1);
                    state.AddPoliceHeat(1);
                    state.AddPublicTrust(1);
                    state.AddLog("Police protected a witness near " + district.DisplayName + ".");
                    break;
                case "police_lockdown":
                    district.SetLockdown(true);
                    district.AddGangInfluence(-1);
                    district.AddPolicePresence(1);
                    state.AddPoliceHeat(1);
                    state.AddPublicTrust(-1);
                    state.AddLog("Police set a checkpoint around " + district.DisplayName + ".");
                    break;
                case "undercover_cover":
                    state.AddCover(10);
                    state.AddSuspicion(-12);
                    district.AddGangInfluence(1);
                    state.AddLog("Undercover maintained cover in " + district.DisplayName + ".");
                    break;
                case "undercover_intel":
                    state.AddEvidence(district.HasWitness ? 2 : 1);
                    state.AddPoliceHeat(1);
                    state.AddCover(-8);
                    state.AddSuspicion(15);
                    district.AddPolicePresence(1);
                    state.AddLog("Undercover passed intel from " + district.DisplayName + ".");
                    break;
                case "undercover_sabotage":
                    state.AddShipmentProgress(-1);
                    state.AddCover(-12);
                    state.AddSuspicion(20);
                    state.AddPoliceHeat(1);
                    district.AddGangInfluence(-1);
                    state.AddLog("Undercover sabotaged gang logistics in " + district.DisplayName + ".");
                    break;
                case "undercover_dead_drop":
                    state.AddEvidence(district.HasWitness ? 2 : 1);
                    state.AddCover(-5);
                    state.AddSuspicion(district.IsLockedDown ? 4 : 8);
                    district.AddCivilianTrust(1);
                    state.AddLog("Undercover completed a dead drop in " + district.DisplayName + ".");
                    break;
            }
        }
    }
}
