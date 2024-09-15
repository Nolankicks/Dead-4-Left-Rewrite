using System;
using Sandbox;
using Sandbox.Citizen;
using Sandbox.Events;

public record DamageEvent( int Amount, GameObject Attacker, GameObject Player, Vector3 HitPos, SceneTraceResult tr = new SceneTraceResult() ) : IGameEvent;

public record PlayerDeath( PlayerController Player ) : IGameEvent;

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

	[Property, Sync, Category( "Stats" )] public int Health { get; set; } = 100;
	[Property, Sync] public ModelRenderer HoldRenderer { get; set; } 
	[Property, Sync] public Inventory Inventory { get; set; } 

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
			Move();

		Crouch();

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

		return Input.Down( "run" ) ? 500 : 300;
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
		TakeDamage( eventArgs.Amount, eventArgs.Attacker, eventArgs.HitPos );
	}

	[Broadcast]
	public void TakeDamage( int amount, GameObject attacker, Vector3 hitPos )
	{
		if ( !IsProxy )
		{
			var health = Health - amount;

			if ( health <= 0 )
				Health = 0;
			else
				Health = health;
		}

		if ( Health <= 0 )
		{
			Scene.Dispatch( new PlayerDeath( this ) );
		}
	}

	void IGameEventHandler<PlayerReset>.OnGameEvent( PlayerReset eventArgs )
	{
		if ( IsProxy )
			return;

		Health = 100;
	}

	[Authority]
	public void SetWorld( Transform transform )
	{
		EyeAngles = transform.Rotation.Angles();

		Transform.World = transform;
	}
}
