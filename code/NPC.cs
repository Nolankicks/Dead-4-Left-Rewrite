using System.Runtime.CompilerServices;
using System.Threading;
using Sandbox;
using Sandbox.Citizen;
using Sandbox.Events;
using Sandbox.Navigation;
using Sandbox.States;

public sealed class NPC : Component, IGameEventHandler<PlayerDeath>, IGameEventHandler<DeathEvent>
{
	[Sync] public Vector3 Destination { get; set; }
	[Property] public NavMeshAgent Agent { get; set; }
	[Sync] public bool IsMoving { get; set; } = false;
	[Sync] public bool WishMove { get; set; } = false;
	[Property] public CitizenAnimationHelper AnimationHelper { get; set; }
	[Sync, Property] public bool Stop { get; set; } = false;
	[Property] public CitizenAnimationHelper.HoldTypes HoldType { get; set; } = CitizenAnimationHelper.HoldTypes.None;
	[Sync] public Vector3 Velocity { get; set; }
	[Sync] public Vector3 WishVelocity { get; set; }
	[RequireComponent] public HealthComponent HealthComponent { get; set; }

	public bool NearPlayer( float distance )
	{
		var nearestPlayer = Scene.GetAllComponents<PlayerController>().FirstOrDefault( x => x.Transform.Position.Distance( Transform.Position ) < distance );

		return nearestPlayer.IsValid();
	}

	public void MoveTo( Vector3 des )
	{
		Destination = des;

		if ( !Agent.IsValid() || Stop )
			return;

		Agent.MoveTo( Destination );

		IsMoving = true;
		WishMove = true;
	}

	[Rpc.Broadcast]
	public void ClearDestination()
	{
		if ( !Networking.IsHost )
			return;

		Destination = Vector3.Zero;
		IsMoving = false;
		WishMove = false;

		if ( !Agent.IsValid() )
			return;

		Agent.Stop();
	}

	public void MoveToPlayer()
	{
		if ( !Agent.IsValid() )
			return;

		var nearestPlayer = NearestPlayer();

		Stop = AtDestination();

		if ( nearestPlayer.IsValid() )
			MoveTo( nearestPlayer.Transform.Position );
	}

	public PlayerController NearestPlayer()
	{
		return Scene.GetAllComponents<PlayerController>().OrderBy( x => x.Transform.Position.Distance( Transform.Position ) ).FirstOrDefault();
	}

	public bool AtDestination( float buffer = 50 )
	{
		if ( !Agent.IsValid() )
			return false;

		return Agent.Transform.Position.Distance( Destination ) < buffer;
	}

	protected override void OnUpdate()
	{
		if ( Networking.IsHost )
		{
			if ( !Agent.IsValid() )
				return;

			Velocity = Agent.Velocity;
			WishVelocity = Agent.WishVelocity;

			if ( Stop )
			{
				Agent.Stop();
				IsMoving = false;
			}

			if ( !IsMoving && !Stop && WishMove )
			{
				Agent.MoveTo( Destination );
				IsMoving = true;
			}

			if ( IsMoving && Agent.Transform.Position.Distance( Destination ) < 250f && !Stop )
			{
				IsMoving = false;
				WishMove = false;

				Agent.Stop();
			}
		}

		if ( !AnimationHelper.IsValid() )
			return;

		AnimationHelper.WithVelocity( Velocity );
		AnimationHelper.WithVelocity( WishVelocity );
		AnimationHelper.HoldType = HoldType;
	}

	public void Wander( bool start )
	{
		if ( start )
			Stop = false;

		if ( IsMoving || !Agent.IsValid() || WishMove || Stop )
			return;

		var randomPoint = Scene.NavMesh?.GetRandomPoint() ?? Vector3.Zero;

		MoveTo( randomPoint );
	}

	public TimeSince lastFired { get; set; } = 0;

	public void Attack( int damage = 10, float fireRate = 5, float length = 200 )
	{
		if ( lastFired < fireRate )
			return;

		var tr = Scene.Trace.Ray( Transform.Position, Transform.Position + Transform.Rotation.Forward * length )
			.WithoutTags( "zombie" )
			.IgnoreGameObject( GameObject )
			.Run();

		if ( tr.Hit && tr.GameObject.Components.TryGet<HealthComponent>( out var healthComponent, FindMode.EverythingInSelfAndParent ) )
		{
			healthComponent.TakeDamage( GameObject, damage, tr.EndPosition, tr.Normal );
		}

		if ( Networking.IsHost )
			BroadcastAttack();

		lastFired = 0;
	}

	[Rpc.Broadcast]
	public void BroadcastAttack()
	{
		if ( AnimationHelper?.Target.IsValid() ?? false )
			AnimationHelper.Target.Set( "b_attack", true );
	}

	void IGameEventHandler<PlayerDeath>.OnGameEvent( PlayerDeath eventArgs )
	{
		if ( !eventArgs.Player.IsValid() )
			return;

		if ( NearestPlayer() == eventArgs.Player )
		{
			ClearDestination();
			BroadcastMessage();
		}

	}

    [Rpc.Broadcast]
    public void BroadcastMessage()
    {
        if ( Components.TryGet<StateMachineComponent>( out var state, FindMode.EverythingInSelfAndDescendants ) && Networking.IsHost )
		{
			state.SendMessage( "restart" );
		}
    }

	void IGameEventHandler<DeathEvent>.OnGameEvent( DeathEvent eventArgs )
	{
		if ( eventArgs.Attacker.Components.TryGet<PlayerController>( out var player ) && eventArgs.Attacker.IsValid() )
			player.AddScore( 5 );

		BroadcastDeath( eventArgs.damageNormal );
	}

	[Rpc.Broadcast]
	public void BroadcastDeath( Vector3 normal )
	{
		if ( !HealthComponent.IsValid() )
			return;

		if ( HealthComponent.Health <= 0 )
		{
			ClearDestination();
		}

		if ( Components.TryGet<StateMachineComponent>( out var state, FindMode.EverythingInSelfAndDescendants ) )
			state.Destroy();

		if ( Components.TryGet<ModelPhysics>( out var physics, FindMode.EnabledInSelfAndDescendants ) )
		{
			physics.MotionEnabled = true;

			physics.PhysicsGroup.AddVelocity( -normal * 300 );
			physics.PhysicsGroup.AddAngularVelocity( Vector3.Random * 300 );
		}

		if ( Components.TryGet<Collider>( out var collider, FindMode.EnabledInSelfAndDescendants ) )
			collider.Destroy();

		Components.Create<Destoryer>();

		Destroy();
	}
}
