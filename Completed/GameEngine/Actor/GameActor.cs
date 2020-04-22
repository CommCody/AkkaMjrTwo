using System;
using System.Collections.Generic;
using Akka;
using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Persistence;
using AkkaMjrTwo.Domain;

namespace AkkaMjrTwo.GameEngine.Actor
{
    public abstract class CommandResult
    { }

    public class CommandAccepted : CommandResult
    { }

    public class CommandRejected : CommandResult
    {
        public GameRuleViolation Violation { get; }

        public CommandRejected(GameRuleViolation violation)
        {
            Violation = violation;
        }
    }


    public class GameActor : PersistentActor
    {
        private Game _game;

        private readonly GameId _id;
        private readonly List<ICancelable> _cancelable;

        public override string PersistenceId => _id.Value;

        public GameActor(GameId id)
        {
            _id = id;
            _game = Game.Create(id);
            _cancelable = new List<ICancelable>();
        }

        public static Props GetProps(GameId id)
        {
            return Props.Create(() => new GameActor(id));
        }

        protected override bool ReceiveCommand(object message)
        {
            return message.Match()
                .With<GameCommand>(cmd => HandleResult(cmd))
                .With<TickCountdown>(cmd => HandleResult(cmd))
                .Default(o =>
                {
                    Context.System.Log.Warning("Game is not running, cannot update countdown");
                    CancelCountdownTick();
                })
                .WasHandled;
        }

        protected override bool ReceiveRecover(object message)
        {
            return message.Match()
                .With<GameEvent>((ev) =>
                {
                    _game = _game.ApplyEvent(ev);
                })
                .With<RecoveryCompleted>(() =>
                {
                    if (_game.IsRunning)
                    {
                        ScheduleCountdownTick();
                    }
                })
                .WasHandled;
        }

        private void HandleResult(TickCountdown command)
        {
            var originalGame = _game;
            _game = originalGame.TickCountDown();
            HandleChanges(originalGame, _game);
        }

        private void HandleResult(GameCommand command)
        {
            try
            {
                var originalGame = _game;
                switch (command)
                {
                    case StartGame startGame:
                        _game = _game.Start(startGame.Players);
                        break;
                    case RollDice rollDice:
                        _game = _game.RollDice(rollDice.Player);
                        break;
                }

                Sender.Tell(new CommandAccepted());

                HandleChanges(originalGame, _game);
            }
            catch (GameRuleViolation violation)
            {
                Sender.Tell(new CommandRejected(violation));
            }
        }

        private void HandleChanges(Game originalGame, Game currentGame)
        {
            PersistAll(currentGame.Events.RemoveRange(0, originalGame.Events.Count), ev =>
            {
                PublishEvent(ev);

                ev.Match()
                  .With<GameStarted>(ScheduleCountdownTick)
                  .With<TurnChanged>(() =>
                  {
                      CancelCountdownTick();
                      ScheduleCountdownTick();
                  })
                  .With<GameFinished>(() =>
                  {
                      CancelCountdownTick();
                      Context.Stop(Self);
                  });
            });
        }

        private void PublishEvent(GameEvent @event)
        {
            var mediator = DistributedPubSub.Get(Context.System).Mediator;
            if (mediator.Equals(ActorRefs.Nobody))
            {
                Log.Error($"Unable to publish event { @event.GetType().Name }. Distributed pub/sub mediator not found.");
                return;
            }
            mediator.Tell(new Publish($"game_event", @event));
        }

        private void ScheduleCountdownTick()
        {
            var cancelable = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1), Self, new TickCountdown(), ActorRefs.NoSender);

            _cancelable.Add(cancelable);
        }

        private void CancelCountdownTick()
        {
            foreach (var cancelable in _cancelable)
            {
                cancelable.Cancel();
            }
            _cancelable.Clear();
        }

        private class TickCountdown
        { }
    }
}
