using Sandbox;

[Title( "NPC Spawner" )]
public sealed class NPCSpawner : Component
{
	[Property] public GameObject NPCPrefab { get; set; }

	public TimeUntil NextSpawn { get; set; } = 0;	

	protected override void OnUpdate()
	{
		if ( NextSpawn && Networking.IsHost )
		{
			Spawn();
		}
	}

	public void Spawn()
	{
		if ( !NPCPrefab.IsValid() || !Scene.IsValid() )
			return;

        if ( Scene.NavMesh is null )
            return;

		var randomPoint = Scene.NavMesh.GetRandomPoint();

        if ( randomPoint is null )
            return;

		var clone = NPCPrefab.Clone();

		clone.Transform.Position = randomPoint.Value;

		clone.NetworkSpawn( null );

		NextSpawn = 3;
	}
}
