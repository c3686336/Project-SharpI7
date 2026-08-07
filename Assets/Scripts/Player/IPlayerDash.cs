public interface IPlayerDash
{
    float DashCooldownUntil { get; }
    float DashCooldownProgress { get; }
    bool IsDashing { get; }
}
