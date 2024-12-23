using Sandbox;
using Sandbox.Events;

public record DamageEvent( int Amount, GameObject Attacker, GameObject Player, Vector3 HitPos = default, Vector3 HitNormal = default ) : IGameEvent;
public record DeathEvent( GameObject Attacker, GameObject Player, Vector3 damagePos, Vector3 damageNormal ) : IGameEvent;

public record GlobalDamageEvent( int Amount, GameObject Attacker, GameObject Player, Vector3 HitPos = default ) : IGameEvent;

public sealed class HealthComponent : Component
{
    [Property, Sync] public int Health { get; set; } = 100;
    [Property] public int MaxHealth { get; set; } = 100;
    [Property, Sync] public bool IsDead { get; set; } = false;

    [Authority]
    public void TakeDamage( GameObject Attacker, int damage = 1, Vector3 HitPos = default, Vector3 normal = default )
    {
        if ( IsDead )
            return;

        var health = Health - damage;

        if ( health <= 0 )
            Health = 0;
        else
            Health = health;

        if ( Health <= 0 )
        {
            IsDead = true;
            GameObject.Dispatch( new DeathEvent( Attacker, GameObject, HitPos, normal ) );
        }

        GameObject.Dispatch( new DamageEvent( damage, Attacker, GameObject, HitPos, normal ) );
    }

	[Rpc.Broadcast]
	public void BroadcastGlobalDamageEvent( int Amount, GameObject Attacker, GameObject Player )
	{
		Scene.Dispatch( new GlobalDamageEvent( Amount, Attacker, Player ) );
	}

    public void ResetHealth()
    {
        Health = MaxHealth;
        IsDead = false;
    }
}
