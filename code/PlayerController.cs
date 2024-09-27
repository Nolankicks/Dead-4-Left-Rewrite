using System;
using Sandbox;
using Sandbox.Citizen;
using Sandbox.Events;
using Sandbox.Services;
using Sandbox.Utility;


public record PlayerDeath( PlayerController Player ) : IGameEvent;

public record PlayerDamage( PlayerController Player, DamageEvent DamageEvent ) : IGameEvent;

public record PlayerReset() : IGameEvent;

public record JumpEvent() : IGameEvent;

public record OnPlayerJoin() : IGameEvent;

public sealed class PlayerController : Component, IGameEventHandler<DamageEvent>, IGameEventHandler<PlayerReset>
{
	[Property, Category( "Refrences" )] public ShrimpleCharacterController.ShrimpleCharacterController shrimpleCharacterController { get; set; }
	[Property, Category( "Refrences" ), Sync] public CitizenAnimationHelper AnimHelper { get; set; }

	public Vector3 WishVelocity { get; set; }

	[Sync] public Angles EyeAngles { get; set; }

	[Property, Category( "Refrences" )] public GameObject Eye { get; set; }

	[Property, Sync] public ModelRenderer HoldRenderer { get; set; }
	[Property, Sync] public Inventory Inventory { get; set; }
	[Property, Sync] public int Score { get; set; }
	[RequireComponent, Sync] public HealthComponent HealthComponent { get; set; }

	private static PlayerController _local;

	public static PlayerController Local
	{
		get
		{
			if ( !_local.IsValid() )
				_local = Game.ActiveScene.GetAllComponents<PlayerController>().FirstOrDefault( x => !x.IsProxy );

			return _local;
		}
	}

	TimeSince lastUngrounded;

	protected override void OnFixedUpdate()
	{
		if ( !IsProxy )
		{
			Crouch();
			Move();
		}

		Anims();

		if ( AnimHelper?.Target.IsValid() ?? false )
			AnimHelper.Target.Transform.Rotation = new Angles( 0, EyeAngles.yaw, 0 ).ToRotation();
	}

	protected override void OnUpdate()
	{
		if ( !IsProxy )
		{
			BuildEyeAngles();
			CameraPos();

			if ( !shrimpleCharacterController.IsValid() )
				return;
		}
	}

	public bool IsCrouching { get; set; } = false;

	bool CanUncrouch()
	{
		if ( !IsCrouching )
			return true;

		if ( lastUngrounded < 0.2f )
			return false;

		var tr = Scene.Trace.Ray( shrimpleCharacterController.Transform.Position, shrimpleCharacterController.Transform.Position + Vector3.Up * 64 )
			.IgnoreGameObject( GameObject )
			.Run();

		return !tr.Hit;
	}

	public void Crouch()
	{
		if ( Input.Down( "duck" ) == IsCrouching )
			return;

		if ( Input.Down( "duck" ) )
		{
			shrimpleCharacterController.TraceHeight = 36;

			IsCrouching = true;

			return;
		}

		if ( !Input.Down( "duck" ) && CanUncrouch() )
		{
			shrimpleCharacterController.TraceHeight = 72;

			IsCrouching = false;

			return;
		}
	}

	public float GetMoveSpeed()
	{
		if ( IsCrouching )
			return 100;

		return Input.Down( "run" ) ? 450 : 300;
	}

	public void Move()
	{
		if ( !shrimpleCharacterController.IsValid() )
			return;

		WishVelocity = Input.AnalogMove.Normal * Rotation.FromYaw( EyeAngles.yaw );

		WishVelocity *= GetMoveSpeed();

		if ( !shrimpleCharacterController.IsOnGround )
		{
			lastUngrounded = 0;
		}

		if ( Input.Pressed( "jump" ) && shrimpleCharacterController.IsOnGround )
		{
			shrimpleCharacterController.Punch( Vector3.Up * 300 );
			GameObject.Dispatch( new JumpEvent() );
		}

		shrimpleCharacterController.WishVelocity = WishVelocity;

		shrimpleCharacterController.Move();
	}

	[Broadcast]
	public void BroadcastJump()
	{
		if ( !AnimHelper.IsValid() )
			return;

		AnimHelper.TriggerJump();
	}

	public void Anims()
	{
		if ( !AnimHelper.IsValid() || !shrimpleCharacterController.IsValid() )
			return;

		AnimHelper.WithVelocity( shrimpleCharacterController.Velocity );
		AnimHelper.WithWishVelocity( WishVelocity );
		AnimHelper.IsGrounded = shrimpleCharacterController.IsOnGround;
		AnimHelper.WithLook( EyeAngles.Forward * 100, 1, 0.5f, 0.25f );

		AnimHelper.DuckLevel = IsCrouching ? 1 : 0;
	}

	public void BuildEyeAngles()
	{
		var ee = EyeAngles;

		ee += Input.AnalogLook;

		ee.pitch = ee.pitch.Clamp( -89, 89 );

		ee.roll = 0;

		EyeAngles = ee;

		Eye.Transform.Rotation = ee.ToRotation();
	}

	public void CameraPos()
	{
		if ( !Scene?.Camera.IsValid() ?? false )
			return;

		var targetPos = Transform.Position + new Vector3( 0, 0, IsCrouching ? 32 : 64 );

		if ( lastUngrounded > 0.2f )
			targetPos.z = Scene.Camera.Transform.Position.z.LerpTo( targetPos.z, Time.Delta * 5 );

		Scene.Camera.Transform.Position = targetPos;

		Eye.Transform.Position = Scene.Camera.Transform.Position;

		Scene.Camera.Transform.Rotation = EyeAngles.ToRotation();
	}

	[Broadcast]
	public void BroadcastAttack()
	{
		if ( !AnimHelper.IsValid() || !AnimHelper.Target.IsValid() )
			return;

		AnimHelper.Target.Set( "b_attack", true );
	}

	protected override void OnPreRender()
	{
		var renderType = IsProxy ? ModelRenderer.ShadowRenderType.On : ModelRenderer.ShadowRenderType.ShadowsOnly;

		var target = AnimHelper.Target;

		if ( target.IsValid() )
			target.RenderType = renderType;

		foreach ( var model in Components.GetAll<ModelRenderer>().Where( x => x.Tags.Has( "clothing" ) ) )
		{
			model.RenderType = renderType;
		}

		if ( HoldRenderer.IsValid() )
			HoldRenderer.RenderType = renderType;
	}

	void IGameEventHandler<DamageEvent>.OnGameEvent( DamageEvent eventArgs )
	{
		TakeDamage();

		Scene.Dispatch( new PlayerDamage( this, eventArgs ) );
	}

	[Broadcast]
	public void TakeDamage()
	{
		if ( !HealthComponent.IsValid() )
			return;

		if ( HealthComponent.Health <= 0 )
		{
			Scene.Dispatch( new PlayerDeath( this ) );

			if ( !IsProxy )
			{
				Stats.Increment( "deaths", 1 );
				Stats.Flush();
			}
		}
	}

	[Authority]
	public void AddScore( int amount )
	{
		if ( IsProxy )
			return;

		Score += amount;

		Stats.Increment( "zombieskilled", Score );
	}

	void IGameEventHandler<PlayerReset>.OnGameEvent( PlayerReset eventArgs )
	{
		if ( IsProxy )
			return;

		if ( HealthComponent.IsValid() )
			HealthComponent.ResetHealth();

		Score = 0;
	}

	[Authority]
	public void SetWorld( Transform transform )
	{
		EyeAngles = transform.Rotation.Angles();

		Transform.World = transform;
	}

	[ConCmd( "kill" )]
	public static void KillPlayer()
	{
		if ( !Local.IsValid() )
			return;

		if ( Local.IsProxy )
			return;

		if ( Local.IsValid() )
			Local.HealthComponent.TakeDamage( Local.GameObject, 100 );
	}
}
