using System;
using GanglandUndercover.Core;

namespace GanglandUndercover.Gameplay
{
    public sealed class GameController
    {
        private readonly ActionResolver actionResolver = new ActionResolver();
        private readonly OpponentAi opponentAi = new OpponentAi();
        private readonly EventResolver eventResolver = new EventResolver();
        private readonly VictoryEvaluator victoryEvaluator = new VictoryEvaluator();

        public GameController()
        {
            State = new GameState();
            SelectedDistrict = DistrictType.Dockyard;
        }

        public GameState State { get; }
        public ActionResolver Actions => actionResolver;
        public DistrictType SelectedDistrict { get; private set; }
        public event Action Changed;

        public void Reset()
        {
            State.Reset();
            SelectedDistrict = DistrictType.Dockyard;
            eventResolver.Reset();
            Changed?.Invoke();
        }

        public void SelectFaction(Faction faction)
        {
            State.SelectFaction(faction);
            Changed?.Invoke();
        }

        public void SelectDistrict(DistrictType districtType)
        {
            SelectedDistrict = districtType;
            Changed?.Invoke();
        }

        public void ToggleLanguage()
        {
            State.ToggleLanguage();
            Changed?.Invoke();
        }

        public void RunPlayerAction(DistrictType districtType, PlayerAction action)
        {
            if (State.Phase != GamePhase.PlayerTurn)
            {
                return;
            }

            State.SetPhase(GamePhase.AiTurn);
            DistrictState district = State.GetDistrict(districtType);
            actionResolver.Resolve(State, district, action);

            if (TryEndGame())
            {
                Changed?.Invoke();
                return;
            }

            opponentAi.Run(State);

            if (TryEndGame())
            {
                Changed?.Invoke();
                return;
            }

            eventResolver.Resolve(State);

            if (TryEndGame())
            {
                Changed?.Invoke();
                return;
            }

            State.AdvanceDay();
            State.SetPhase(GamePhase.PlayerTurn);
            Changed?.Invoke();
        }

        private bool TryEndGame()
        {
            if (!victoryEvaluator.TryEvaluate(State, out string result))
            {
                return false;
            }

            State.Finish(result);
            return true;
        }
    }
}
