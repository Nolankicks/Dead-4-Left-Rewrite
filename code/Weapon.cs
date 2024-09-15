using Sandbox;
using Sandbox.Citizen;
using Sandbox.Events;

public sealed class Weapon : Component, IGameEventHandler<OnPlayerJoin>, IGameEventHandler<OnItemEquipped>
{
	[Property] public float FireRate { get; set; } = 0.1f;
	[Property] public int Damage { get; set; } = 10;
	TimeSince lastFired { get; set; }
	[Property, Sync] public SkinnedModelRenderer Renderer { get; set; }
	[Property] public Model WorldModel { get; set; }
	[Property, Sync] public CitizenAnimationHelper.HoldTypes HoldType { get; set; }
	[Property, Sync] public Vector3 Offset { get; set; }
	[Property] public float Range { get; set; } = 5000;
	[Property] public float Spread { get; set; } = 0.03f;
	[Property] public int TraceTimes { get; set; } = 1;

	protected override void OnEnabled()
	{
		if ( !IsProxy )
		{
			lastFired = FireRate;
		}
	}

	protected override void OnStart()
	{
		if ( IsProxy )
			return;
		
		BroadcastEquip( PlayerController.Local );
	}

	void IGameEventHandler<OnPlayerJoin>.OnGameEvent( OnPlayerJoin eventArgs )
	{
		if ( IsProxy || !GameObject.Enabled )
			return;

		BroadcastEquip( PlayerController.Local );
	}

	protected override void OnUpdate()
	{
		if ( Input.Down( "attack1" ) && !IsProxy && lastFired > FireRate )
		{
			for ( var i = 0; i < TraceTimes; i++ )
				Shoot();

			if ( Renderer.IsValid() )
				Renderer.Set( "b_attack", true );

			lastFired = 0;
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
			return;


	}

	public void Shoot()
	{
		var local = PlayerController.Local;

		if ( !local.IsValid() )
			return;

		var tr = Scene.Trace.Ray( local.Eye.Transform.Position, local.Eye.Transform.Position + ( local.Eye.Transform.Rotation.Forward + Vector3.Random * Spread ) * Range )
			.IgnoreGameObject( local.GameObject )
			.Run();

		local.EyeAngles += new Angles( Game.Random.Float( -1, 1 ), Game.Random.Float( -1, 1 ), 0 );

		if ( !tr.Hit )
			return;

		if ( tr.GameObject.Components.TryGet<PlayerController>( out var player, FindMode.EverythingInSelfAndParent ) && GameSystem.PVP )
		{
			player.GameObject.Dispatch( new DamageEvent( Damage, local.GameObject, player.GameObject, tr.EndPosition, tr ) );
		}

		if ( tr.GameObject.Components.TryGet<NPC>( out var npc, FindMode.EverythingInSelfAndParent ) )
		{
			npc.GameObject.Dispatch( new DamageEvent( Damage, local.GameObject, npc.GameObject, tr.EndPosition, tr ) );
		}

		if ( tr.Body.IsValid() )
		{
			tr.Body.ApplyImpulseAt( tr.HitPosition, tr.Direction * 200.0f * tr.Body.Mass.Clamp( 0, 200 ) );
		}

		var damage = new DamageInfo( Damage, GameObject, GameObject, tr.Hitbox );
		damage.Position = tr.HitPosition;
		damage.Shape = tr.Shape;

		foreach ( var damageable in tr.GameObject.Components.GetAll<IDamageable>() )
		{
			damageable.OnDamage( damage );
		}
	}

	[Broadcast]
	public void BroadcastEquip( PlayerController local )
	{
		if ( !local.IsValid() )
			return;

		local.HoldRenderer.Model = WorldModel;
		local.AnimHelper.HoldType = HoldType;
		local.HoldRenderer.Transform.LocalPosition = Offset;
	}

	void IGameEventHandler<OnItemEquipped>.OnGameEvent( OnItemEquipped eventArgs )
	{
		if ( IsProxy )
			return;

		BroadcastEquip( PlayerController.Local );
	}
}

[GameResource( "Weapon Data", "weapons", "Data for a weapon" )]
public sealed class WeaponData : GameResource
{
	public string Name { get; set; }
	public Texture Icon { get; set; }
	public GameObject WeaponPrefab { get; set; }
}
