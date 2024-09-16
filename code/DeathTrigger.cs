using Sandbox;
using Sandbox.Events;

public sealed class DeathTrigger : Component, Component.ITriggerListener
{
	public void OnTriggerEnter( Collider other )
	{
		if ( other.GameObject.Components.TryGet<PlayerController>( out var player, FindMode.EverythingInSelfAndParent ) )
		{
			player.GameObject.Dispatch( new DamageEvent( player.Health, GameObject, player.GameObject, Transform.Position ) );
		}
	}
}
