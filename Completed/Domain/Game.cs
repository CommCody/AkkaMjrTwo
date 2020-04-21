using AkkaMjrTwo.Domain.Config;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace AkkaMjrTwo.Domain
{
    public abstract class Game : AggregateRoot<Game, GameEvent>
    {
        public bool IsFinished => this is FinishedGame;
        public bool IsRunning => this is RunningGame;

        protected GameId GameId => Id as GameId;

        protected Game(GameId id)
            : base(id)
        { }

        protected Game(GameId id, ImmutableList<GameEvent> events)
            : base(id, events)
        { }

        public static UninitializedGame Create(GameId id)
        {
            return new UninitializedGame(id);
        }

        public Game HandleCommand(GameCommand command)
        {
            if (command is StartGame game)
            {
                if (this is UninitializedGame uninitializedGame)
                {
                    return uninitializedGame.Start(game.Players);
                }
                else throw new GameAlreadyStartedViolation();
            }

            if (command is RollDice dice)
            {
                if (this is RunningGame runningGame)
                {
                    return runningGame.Roll(dice.Player);
                }
                else throw new GameNotRunningViolation();
            }
            return this;
        }
    }

    public class UninitializedGame : Game
    {
        public UninitializedGame(GameId id)
            : base(id)
        {
        }

        public Game Start(ImmutableList<PlayerId> players)
        {
            if (players.Count < 2)
            {
                throw new NotEnoughPlayersViolation();
            }

            var firstPlayer = players[0];

            return ApplyEvent(new GameStarted(GameId, players, new Turn(firstPlayer, GlobalSettings.TurnTimeoutSeconds)));
        }

        public override Game ApplyEvent(GameEvent @event)
        {
            if (@event is GameStarted gameStarted)
            {
                return new RunningGame(GameId, gameStarted.Players, gameStarted.InitialTurn, Events.Add(@event));
            }

            return this;
        }
    }



    public class RunningGame : Game
    {
        private readonly Random _random;
        private readonly ImmutableList<(PlayerId Player, int Roll)> _rolledNumbers;
        private readonly ImmutableList<PlayerId> _players;
        private readonly Turn _turn;

        public RunningGame(GameId id, ImmutableList<PlayerId> players, Turn turn, ImmutableList<GameEvent> events)
            : this(id, players, ImmutableList.Create<(PlayerId, int)>(), turn, events)
        { }

        private RunningGame(GameId id, ImmutableList<PlayerId> players, ImmutableList<(PlayerId, int)> rolledNumbers, Turn turn, ImmutableList<GameEvent> events)
            : base(id, events)
        {
            _random = new Random();
            _rolledNumbers = rolledNumbers;
            _players = players;
            _turn = turn;
        }

        public Game Roll(PlayerId player)
        {
            if (_turn.CurrentPlayer.Equals(player))
            {
                var rolledNumber = _random.Next(1, 7);
                var diceRolled = new DiceRolled(GameId, rolledNumber, player);

                var game = Apply(diceRolled);

                var nextPlayer = game.GetNextPlayer();
                if (nextPlayer != null)
                {
                    return game.ApplyEvent(new TurnChanged(GameId, new Turn(nextPlayer, GlobalSettings.TurnTimeoutSeconds)));
                }
                else
                {
                    return game.ApplyEvent(new GameFinished(GameId, game.BestPlayers()));
                }
            }
            else throw new NotCurrentPlayerViolation();
        }

        public Game TickCountDown()
        {
            if (_turn.SecondsLeft > 1)
            {
                return ApplyEvent(new TurnCountdownUpdated(GameId, _turn.SecondsLeft - 1));
            }
            var timedOut = new TurnTimedOut(GameId);
            var game = Apply(timedOut);
            var nextPlayer = game.GetNextPlayer();
            if (nextPlayer != null)
            {
                return game.ApplyEvent(new TurnChanged(GameId, new Turn(nextPlayer, GlobalSettings.TurnTimeoutSeconds)));
            }
            else
            {
                return game.ApplyEvent(new GameFinished(GameId, game.BestPlayers()));
            }
        }
        
        private RunningGame Apply(DiceRolled diceRolled)
        {
            if (!_rolledNumbers.Exists(x => x.Player.Equals(diceRolled.Player)))
            {
                var rolledNumbers = _rolledNumbers.Add((diceRolled.Player, diceRolled.RolledNumber));
                return new RunningGame(GameId, _players, rolledNumbers, _turn, Events.Add(diceRolled));
            }
            return this;
        }

        private RunningGame Apply(TurnTimedOut turnTimedOut)
        {
            return new RunningGame(GameId, _players, _rolledNumbers, _turn, Events.Add(turnTimedOut));
        }

        public override Game ApplyEvent(GameEvent @event)
        {
            if (@event is DiceRolled diceRolled)
            {
                return Apply(diceRolled);
            }
            if (@event is TurnChanged turnChanged)
            {
                return new RunningGame(GameId, _players, _rolledNumbers, turnChanged.Turn, Events.Add(turnChanged));
            }
            if (@event is TurnCountdownUpdated turnCountdownUpdated)
            {
                var updatedTurn = new Turn(_turn.CurrentPlayer, turnCountdownUpdated.SecondsLeft);
                return new RunningGame(GameId, _players, _rolledNumbers, updatedTurn, Events.Add(turnCountdownUpdated));
            }
            if (@event is GameFinished gameFinished)
            {
                return new FinishedGame(GameId, _players, gameFinished.Winners, Events.Add(gameFinished));
            }
            if (@event is TurnTimedOut turnTimedOut)
            {
                return Apply(turnTimedOut);
            }

            return this;
        }

        private ImmutableList<PlayerId> BestPlayers()
        {
            if (!_rolledNumbers.Any())
                return ImmutableList.Create<PlayerId>();

            var highest = _rolledNumbers.Max(x => x.Roll);
            var best = _rolledNumbers.Where(x => x.Roll == highest).Select(x => x.Player).ToImmutableList();

            return best;
        }

        private PlayerId GetNextPlayer()
        {
            var currentPlayerIndex = _players.IndexOf(_turn.CurrentPlayer);
            var nextPlayerIndex = currentPlayerIndex + 1;

            return _players.ElementAtOrDefault(nextPlayerIndex);
        }
    }

    public class FinishedGame : Game
    {
        public ImmutableList<PlayerId> Players { get; }
        public ImmutableList<PlayerId> Winners { get; }

        public FinishedGame(GameId id, ImmutableList<PlayerId> players, ImmutableList<PlayerId> winners, ImmutableList<GameEvent> events)
            : base(id, events)
        {
            Players = players;
            Winners = winners;
        }

        public override Game ApplyEvent(GameEvent arg)
        {
            return this;
        }
    }

    public class Turn
    {
        public PlayerId CurrentPlayer { get; }
        public int SecondsLeft { get; }

        public Turn(PlayerId currentPlayer, int secondsLeft)
        {
            CurrentPlayer = currentPlayer;
            SecondsLeft = secondsLeft;
        }
    }
}
