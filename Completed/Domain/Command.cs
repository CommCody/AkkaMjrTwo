using System.Collections.Immutable;

namespace AkkaMjrTwo.Domain
{
    public abstract class GameCommand
    { }

    public class StartGame : GameCommand
    {
        public ImmutableList<PlayerId> Players { get; }

        public StartGame(ImmutableList<PlayerId> players)
        {
            Players = players;
        }
    }

    public class RollDice : GameCommand
    {
        public PlayerId Player { get; }

        public RollDice(PlayerId player)
        {
            Player = player;
        }
    }
}
