namespace Shenok.Back.IdentityService.Domain.Aggregates.Users
{
    public sealed record PasswordHash
    {
        /*this class doesn't take a text and hashing it!
         * it's only wraps and already hashed STRING!
         * because hashing algorythm choice is an INFRASTRUCTURE decision
         * the domain rule = user always MUST have a non-empty hash password and NEVER has a simple text password.
         * Hashing happens via IPasswordHasher (in Services/) called via Application layer and result is handed to this ValueObject.
         */
        public string Value { get; }
        private PasswordHash(string value) => Value = value;
        /// <summary>
        /// Wraps an already-hashed value. The domain never hashes passwords itself —
        /// that's delegated to IPasswordHasher (Infrastructure). This factory exists
        /// only to guarantee a PasswordHash is never constructed from a null/empty string.
        /// </summary>
        /// 
        public static PasswordHash FromHashedValue(string hashedValue)
        {
            if (string.IsNullOrWhiteSpace(hashedValue))
                throw new ArgumentException("Password cannot be empty!",nameof(hashedValue));
            return new PasswordHash(hashedValue);
        }
        public override string ToString() => Value;
    }
}
