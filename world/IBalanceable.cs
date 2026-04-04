namespace godototype.world;

public interface IBalanceable
{
    bool IsValid();
    void ApplyBalance(double delta);
}
