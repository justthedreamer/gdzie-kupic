namespace Gdzie.Kupic.Domain.Seeding;

/// <summary>
/// Fixed, well-known identifiers and (where applicable) credentials for the mock Admin/Buyer/Merchant
/// accounts seeded at startup. IDs are deterministic (not <see cref="Guid.NewGuid"/>) so that a JWT
/// access token can be pre-generated once and committed to documentation, and its `sub` claim will
/// always match the corresponding seeded account regardless of which environment seeds it.
///
/// This is a deliberate, accepted trade-off specific to this academic project, which never runs
/// against real user data in production. The same approach would be a security defect in a
/// production-bound project.
/// </summary>
public static class MockAccounts
{
    public static class Admin
    {
        public static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
    }

    public static class Buyer
    {
        public static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-000000000002");
        public const string Email = "buyer-test@gdziekupic.local";
        public const string Password = "Buyer123!";
    }

    public static class Merchant
    {
        public static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-000000000003");
        public const string Email = "merchant-test@gdziekupic.local";
        public const string Password = "Merchant123!";
    }
}
