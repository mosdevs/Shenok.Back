namespace Shenok.Back.IdentityService.Domain.Aggregates.Users
{
    public sealed record UserId
    {
        public Guid Value { get;}
        private UserId(Guid value) => Value = value; //internal and safe constructing
        public static UserId New() => new(Guid.NewGuid()); //creating a brand-new user
        public static UserId From(Guid value)
        {
            if (value == Guid.Empty)
                throw new ArgumentException("UserId can't be empty!",nameof(value));
            return new UserId(value); //when u reconstructing from persistence
        }
        public override string ToString() => Value.ToString(); //?`
    }
}
