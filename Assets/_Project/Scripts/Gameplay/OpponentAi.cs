using System.Linq;
using GanglandUndercover.Core;

namespace GanglandUndercover.Gameplay
{
    public sealed class OpponentAi
    {
        public void Run(GameState state)
        {
            if (state.PlayerFaction != Faction.Gang)
            {
                RunGangTurn(state);
            }

            if (state.PlayerFaction != Faction.Police)
            {
                RunPoliceTurn(state);
            }

            if (state.PlayerFaction != Faction.Undercover)
            {
                RunUndercoverPressure(state);
            }
        }

        private static void RunGangTurn(GameState state)
        {
            DistrictState target = state.Districts
                .OrderByDescending(district => district.PolicePresence - district.GangInfluence)
                .First();

            if (target.IsLockedDown)
            {
                target.SetLockdown(false);
                target.AddGangInfluence(1);
                target.AddPolicePresence(-1);
                state.AddPoliceHeat(1);
                state.AddLog("AI Gang bribed through a checkpoint in " + target.DisplayName + ".");
                return;
            }

            if (state.ShipmentProgress < 3 && state.Day % 2 == 0)
            {
                state.AddShipmentProgress(1);
                state.AddPoliceHeat(1);
                target.AddGangInfluence(1);
                state.AddLog("AI Gang advanced the port shipment.");
                return;
            }

            target.AddGangInfluence(1);
            target.AddCivilianTrust(-1);
            state.AddLog("AI Gang pushed influence in " + target.DisplayName + ".");
        }

        private static void RunPoliceTurn(GameState state)
        {
            DistrictState target = state.Districts
                .OrderByDescending(district => district.GangInfluence)
                .First();

            if (!target.IsLockedDown && state.PoliceHeat >= 5)
            {
                target.SetLockdown(true);
                target.AddGangInfluence(-1);
                target.AddPolicePresence(1);
                state.AddPublicTrust(-1);
                state.AddLog("AI Police locked down " + target.DisplayName + ".");
                return;
            }

            if (target.HasWitness || state.Day % 2 == 1)
            {
                state.AddEvidence(target.HasWitness ? 2 : 1);
                state.AddPoliceHeat(1);
                target.AddPolicePresence(1);
                state.AddLog("AI Police investigated " + target.DisplayName + ".");
                return;
            }

            target.AddGangInfluence(-1);
            target.AddPolicePresence(1);
            state.AddPoliceHeat(1);
            state.AddPublicTrust(-1);
            state.AddLog("AI Police raided " + target.DisplayName + ".");
        }

        private static void RunUndercoverPressure(GameState state)
        {
            if (state.Evidence < 6)
            {
                state.AddEvidence(1);
                state.AddSuspicion(10);
                state.AddCover(-5);
                state.AddLog("AI Undercover leaked a partial lead.");
                return;
            }

            state.AddSuspicion(-5);
            state.AddCover(4);
            state.AddLog("AI Undercover kept cover intact.");
        }
    }
}
