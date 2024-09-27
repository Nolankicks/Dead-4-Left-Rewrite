using Sandbox;
using Sandbox.Events;

public sealed class DeathTrigger : Component, Component.ITriggerListener
{
	public void OnTriggerEnter( Collider other )
	{
		if ( other.GameObject.Components.TryGet<HealthComponent>( out var health, FindMode.EverythingInSelfAndParent ) )
		{
			health.TakeDamage( GameObject, 100 );
		}
	}
}
