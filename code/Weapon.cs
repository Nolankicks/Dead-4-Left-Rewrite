using Sandbox;
using Sandbox.Citizen;
using Sandbox.Events;


public class Item : Component, IGameEventHandler<OnItemEquipped>
{
    [Property, Sync] public CitizenAnimationHelper.HoldTypes HoldType { get; set; }
    [Property] public Model WorldModel { get; set; }
    [Property, Sync] public Vector3 Offset { get; set; }

    public virtual void OnEquip( OnItemEquipped onItemEquipped ) { }

    void IGameEventHandler<OnItemEquipped>.OnGameEvent( OnItemEquipped eventArgs )
    {
        OnEquip( eventArgs );

        var player = PlayerController.Local;

        if ( IsProxy || !player.IsValid() )
            return;

        player.HoldType = HoldType;

		player.ChangeHoldType( player.AnimHelper, HoldType );

        BroadcastEquip( player );
    }

    [Rpc.Broadcast]
    public void BroadcastEquip( PlayerController local )
    {
        if ( !local.IsValid() )
            return;

        local.HoldRenderer.Model = WorldModel;
        local.HoldRenderer.Transform.LocalPosition = Offset;
    }
}

public sealed class Weapon : Item
{
    [Property] public float FireRate { get; set; } = 0.1f;
    [Property] public int Damage { get; set; } = 10;
    TimeSince lastFired { get; set; }
    TimeSince EquipTime { get; set; }
    [Property, Sync] public SkinnedModelRenderer Renderer { get; set; }
    [Property] public float Range { get; set; } = 5000;
    [Property] public float Spread { get; set; } = 0.03f;
    [Property] public int TraceTimes { get; set; } = 1;
    [Property] public SoundEvent FireSound { get; set; }
	[Property] public GameObject BloodPrefab { get; set; }

    public override void OnEquip( OnItemEquipped onItemEquipped )
    {
        if ( IsProxy )
            return;

        EquipTime = 0;
        lastFired = FireRate;
    }

    protected override void OnUpdate()
    {
        if ( IsProxy || EquipTime < 0.2f )
            return;

        if ( Input.Down( "attack1" ) && lastFired > FireRate )
        {
            for ( var i = 0; i < TraceTimes; i++ )
                Shoot();

            var local = PlayerController.Local;

            if ( local.IsValid() )
            {
                BroadcastFireSound( local.Eye.Transform.Position );

                local.BroadcastAttack();
            }

            if ( Renderer.IsValid() )
                Renderer.Set( "b_attack", true );

            lastFired = 0;
        }
    }

    public void Shoot()
    {
        var local = PlayerController.Local;

        var cam = Scene.Camera;

        if ( !local.IsValid() || !cam.IsValid() )
            return;

        var ray = cam.ScreenNormalToRay( 0.5f );

        ray.Forward += Vector3.Random * Spread;

        var tr = Scene.Trace.Ray( ray, Range )
            .IgnoreGameObjectHierarchy( local.GameObject )
            .Run();

        local.EyeAngles += new Angles( Game.Random.Float( -1, 1 ), Game.Random.Float( -1, 1 ), 0 );

        if ( !tr.Hit )
            return;

        if ( tr.GameObject.Components.TryGet<HealthComponent>( out var health, FindMode.EverythingInSelfAndParent ) )
        {
            if ( tr.GameObject.Components.TryGet<PlayerController>( out var player, FindMode.EverythingInSelfAndParent ) && !GameSystem.PVP )
                return;

            health.TakeDamage( local.GameObject, Damage, tr.EndPosition, tr.Normal );

            //SpawnParticleEffect( tr.EndPosition );
        }

        if ( tr.Body.IsValid() )
        {
            tr.Body?.ApplyImpulseAt( tr.HitPosition, tr.Direction * 200.0f * tr.Body.Mass.Clamp( 0, 200 ) );
        }

        var damage = new DamageInfo( Damage, GameObject, GameObject, tr.Hitbox );
        damage.Position = tr.HitPosition;
        damage.Shape = tr.Shape;

        foreach ( var damageable in tr.GameObject?.Components.GetAll<IDamageable>() )
        {
            damageable?.OnDamage( damage );
        }
    }

    [Rpc.Broadcast]
    public void BroadcastFireSound( Vector3 pos )
    {
        if ( FireSound is null )
            return;

        var sound = Sound.Play( FireSound, pos );

        if ( !sound.IsValid() )
            return;

        sound.Volume *= 5f;
    }

    public void SpawnParticleEffect( Vector3 pos )
    {
        if ( BloodPrefab is null )
			return;

		var blood = BloodPrefab?.Clone();

		blood.WorldPosition = pos;

		var destroyer = blood.Components.Create<Destoryer>();
		destroyer.Time = 1;
    }
}

[GameResource( "Weapon Data", "weapons", "Data for a weapon" )]
public sealed class WeaponData : GameResource
{
    public string Name { get; set; }
    public Texture Icon { get; set; }
    public GameObject WeaponPrefab { get; set; }
}
