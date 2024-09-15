using System.Threading;
using Sandbox;
using Sandbox.Citizen;
using Sandbox.Events;
using Sandbox.Navigation;
using Sandbox.States;

public sealed class NPC : Component, IGameEventHandler<PlayerDeath>, IGameEventHandler<DamageEvent>
{
	[Sync] public Vector3 Destination { get; set; }
	[Property] public NavMeshAgent Agent { get; set; }
	[Sync] public bool IsMoving { get; set; } = false;
	[Sync] public bool WishMove { get; set; } = false;
	[Property] public CitizenAnimationHelper AnimationHelper { get; set; }
	[Sync, Property] public bool Stop { get; set; } = false;
	[Property] public CitizenAnimationHelper.HoldTypes HoldType { get; set; } = CitizenAnimationHelper.HoldTypes.None;
	[Sync] public int Health { get; set; } = 100;
	[Sync] public bool IsDead { get; set; } = false;
	[Property] public bool HasHealth { get; set; } = false;
	[Sync] public Vector3 Velocity { get; set; }
	[Sync] public Vector3 WishVelocity { get; set; }

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

	public void ClearDestination()
	{
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

	public void Wander()
	{
		if ( IsMoving || !Agent.IsValid() || WishMove || Stop )
			return;

		var randomPoint = Scene.NavMesh.GetRandomPoint().GetValueOrDefault();

		if ( randomPoint == Vector3.Zero )
			return;

		MoveTo( randomPoint );
	}

	public TimeSince lastFired { get; set; } = 0;

	public void Attack( int damage = 10, float fireRate = 5, float length = 200 )
	{
		if ( lastFired < fireRate )
			return;

		var tr = Scene.Trace.Ray( Transform.Position, Transform.Position + Transform.Rotation.Forward * length )
			.IgnoreGameObject( GameObject )
			.Run();

		if ( tr.Hit && tr.GameObject.Components.TryGet<PlayerController>( out var player, FindMode.EverythingInSelfAndParent ) )
		{
			player.GameObject.Dispatch( new DamageEvent( damage, GameObject, player.GameObject, tr.EndPosition ) );
		}

		if ( Networking.IsHost )
			BroadcastAttack();

		lastFired = 0;
	}

	[Broadcast]
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
			ClearDestination();

		if ( Components.TryGet<StateMachineComponent>( out var state, FindMode.EverythingInSelfAndDescendants ) && Networking.IsHost )
		{
			state.SendMessage( "restart" );
		}
	}

	void IGameEventHandler<DamageEvent>.OnGameEvent( DamageEvent eventArgs )
	{
		if ( IsDead )
			return;

		if ( eventArgs.Attacker.Components.TryGet<PlayerController>( out var player ) && eventArgs.Attacker.IsValid() )
		{
			player.AddScore( 5 );
		}

		BroadcastDeath( eventArgs.Amount, eventArgs.tr.Normal );
	}

	[Broadcast]
	public void BroadcastDeath( int amount, Vector3 normal )
	{
		var health = Health - amount;

		if ( IsDead )
			return;

		if ( health <= 0 )
		{
			Health = 0;

			ClearDestination();

			IsDead = true;
		}

		Health = health;

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
