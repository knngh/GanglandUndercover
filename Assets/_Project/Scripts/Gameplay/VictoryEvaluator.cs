using GanglandUndercover.Core;

namespace GanglandUndercover.Gameplay
{
    public sealed class VictoryEvaluator
    {
        public bool TryEvaluate(GameState state, out string result)
        {
            if (state.Suspicion >= 100)
            {
                result = "Gang wins: the undercover identity was exposed.";
                return true;
            }

            if (state.Cover <= 0)
            {
                result = "Gang wins: the undercover cover story collapsed.";
                return true;
            }

            if (state.GangControlledDistricts >= 4)
            {
                result = "Gang wins: four districts are under gang control.";
                return true;
            }

            if (state.ShipmentProgress >= 3 && state.Evidence < 5)
            {
                result = "Gang wins: the major shipment escaped before police built a case.";
                return true;
            }

            if (state.Evidence >= 8 && state.PlayerFaction == Faction.Undercover && state.Cover >= 35 && state.Suspicion < 100)
            {
                result = "Undercover wins: enough evidence was delivered without burning cover.";
                return true;
            }

            if (state.Evidence >= 8 && state.PoliceHeat >= 6)
            {
                result = "Police wins: evidence and heat are enough for a coordinated arrest.";
                return true;
            }

            if (state.Day > 4 && state.GangControlledDistricts <= 1)
            {
                result = "Police wins: gang control collapsed after sustained enforcement.";
                return true;
            }

            if (state.Day > 10)
            {
                result = "Stalemate: the district is unstable and no faction secured a clean win.";
                return true;
            }

            result = string.Empty;
            return false;
        }
    }
}
