using System.Collections.Immutable;

namespace AkkaMjrTwo.Domain
{
    public abstract class AggregateRoot<T, E>
        where T : AggregateRoot<T, E>
    {
        public ImmutableList<E> Events { get; }

        protected Id<T> Id { get; }

        protected AggregateRoot(Id<T> id)
            : this(id, ImmutableList.Create<E>())
        { }

        protected AggregateRoot(Id<T> id, ImmutableList<E> events)
        {
            Id = id;
            Events = events;
        }

        public abstract T ApplyEvent(E @event);
    }
}
