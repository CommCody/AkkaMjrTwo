using Akka.Actor;
using System;
using AkkaMjrTwo.Domain;

namespace AkkaMjrTwo.GameEngine.Actor
{
    #region Messages

    public class CreateGame
    { }



    public class SendCommand
    {
        public GameId GameId { get; }
        public GameCommand Command { get; }

        public SendCommand(GameId gameId, GameCommand command)
        {
            GameId = gameId;
            Command = command;
        }
    }


    public class GameCreated
    {
        public GameId GameId { get; }

        public GameCreated(GameId gameId)
        {
            GameId = gameId;
        }
    }


    public class GameDoesNotExist
    { }


    public class GameAlreadyExists
    { }

    #endregion
       
    public class GameManagerActor : ReceiveActor
    {
        public GameManagerActor()
        {
            Receive<CreateGame>(Handle);
            Receive<SendCommand>(Handle);
        }

        public static Props GetProps()
        {
            return Props.Create<GameManagerActor>();
        }

        private bool Handle(CreateGame message)
        {
            var id = new GameId($"Game_{Guid.NewGuid():D}");

            var gameActor = Context.Child(id.Value);
            if (!gameActor.Equals(ActorRefs.Nobody))
            {
                Sender.Tell(new GameAlreadyExists());
                return true;
            }

            Context.ActorOf(GameActor.GetProps(id), id.Value);

            Sender.Tell(new GameCreated(id));

            return true;
        }

        private bool Handle(SendCommand message)
        {
            var game = Context.Child(message.GameId.Value);
            if (!game.Equals(ActorRefs.Nobody))
            {
                game.Forward(message.Command);
            }
            else
            {
                Sender.Tell(new GameDoesNotExist());
            }

            return true;
        }
    }
}
