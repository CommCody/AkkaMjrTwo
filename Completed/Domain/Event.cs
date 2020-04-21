using System.Collections.Immutable;

namespace AkkaMjrTwo.Domain
{
    public abstract class GameEvent
    {
        public GameId Id { get; }

        protected GameEvent(GameId id)
        {
            Id = id;
        }
    }

    public class DiceRolled : GameEvent
    {
        public int RolledNumber { get; }

        public PlayerId Player { get; }

        public DiceRolled(GameId id, int rolledNumber, PlayerId player)
            : base(id)
        {
            RolledNumber = rolledNumber;
            Player = player;
        }
    }

    public class GameStarted : GameEvent
    {
        public ImmutableList<PlayerId> Players { get; }
        public Turn InitialTurn { get; }

        public GameStarted(GameId id, ImmutableList<PlayerId> players, Turn initialTurn)
            : base(id)
        {
            Players = players;
            InitialTurn = initialTurn;
        }
    }

    public class TurnChanged : GameEvent
    {
        public Turn Turn { get; }

        public TurnChanged(GameId id, Turn turn)
            : base(id)
        {
            Turn = turn;
        }
    }

    public class TurnCountdownUpdated : GameEvent
    {
        public int SecondsLeft { get; }

        public TurnCountdownUpdated(GameId id, int secondsLeft)
            : base(id)
        {
            SecondsLeft = secondsLeft;
        }
    }

    public class TurnTimedOut : GameEvent
    {
        public TurnTimedOut(GameId id)
            : base(id)
        { }
    }

    public class GameFinished : GameEvent
    {
        public ImmutableList<PlayerId> Winners { get; }

        public GameFinished(GameId id, ImmutableList<PlayerId> winners)
            : base(id)
        {
            Winners = winners;
        }
    }
}
