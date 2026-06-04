using System.Collections.Generic;
using GanglandUndercover.Core;

namespace GanglandUndercover.Gameplay
{
    public sealed class EventResolver
    {
        private readonly HashSet<string> triggeredEvents = new HashSet<string>();

        public void Reset()
        {
            triggeredEvents.Clear();
        }

        public void Resolve(GameState state)
        {
            TryTriggerDockyardWitness(state);
            TryTriggerPublicBacklash(state);
            TryTriggerLoyaltyTest(state);
            TryTriggerBossConvoy(state);
        }

        private void TryTriggerDockyardWitness(GameState state)
        {
            if (triggeredEvents.Contains("dockyard_witness") || state.Day < 2 || state.ShipmentProgress < 1)
            {
                return;
            }

            DistrictState dockyard = state.GetDistrict(DistrictType.Dockyard);
            dockyard.SetWitness(true);
            state.AddEvidence(1);
            Trigger(state, new StoryEvent(
                "dockyard_witness",
                "Dockyard Witness",
                "A frightened crane operator saw the shipment route and can now support the case."));
        }

        private void TryTriggerPublicBacklash(GameState state)
        {
            if (triggeredEvents.Contains("public_backlash") || state.PoliceHeat < 8)
            {
                return;
            }

            state.AddPublicTrust(-2);
            state.AddPoliceHeat(-2);
            Trigger(state, new StoryEvent(
                "public_backlash",
                "Public Backlash",
                "Aggressive enforcement triggered protests, lowering public trust but cooling police pressure."));
        }

        private void TryTriggerLoyaltyTest(GameState state)
        {
            if (triggeredEvents.Contains("loyalty_test") || state.Suspicion < 70)
            {
                return;
            }

            state.AddCover(-10);
            state.AddSuspicion(10);
            Trigger(state, new StoryEvent(
                "loyalty_test",
                "Loyalty Test",
                "The crew demanded proof of loyalty. Cover took damage and suspicion rose."));
        }

        private void TryTriggerBossConvoy(GameState state)
        {
            if (triggeredEvents.Contains("boss_convoy") || state.Day < 4 || state.GangControlledDistricts < 3)
            {
                return;
            }

            DistrictState warehouse = state.GetDistrict(DistrictType.WarehouseRow);
            warehouse.AddGangInfluence(1);
            state.AddShipmentProgress(1);
            state.AddPoliceHeat(1);
            Trigger(state, new StoryEvent(
                "boss_convoy",
                "Boss Convoy",
                "Gang control enabled a protected convoy through Warehouse Row, accelerating the shipment."));
        }

        private void Trigger(GameState state, StoryEvent storyEvent)
        {
            triggeredEvents.Add(storyEvent.Id);
            state.AddLog(storyEvent.FormatLog());
        }
    }
}
