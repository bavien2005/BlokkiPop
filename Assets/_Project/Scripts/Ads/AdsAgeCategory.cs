/// <summary>
/// Coarse, local-only audience decision. This is not legal consent, a birth date,
/// or a guarantee that every jurisdiction treats the player as an adult.
/// </summary>
public enum AdsAgeCategory
{
    Unknown = 0,
    Under13 = 1,
    Teen13To17 = 2,
    Adult18Plus = 3
}

public enum RewardedAdResult
{
    Earned,
    ClosedWithoutReward,
    Unavailable,
    Failed,
    Cancelled,
    // Never returned by a real SDK callback. A separate, explicit development
    // action uses this value so simulated rewards cannot be counted as revenue.
    DevelopmentRewardEarned
}
