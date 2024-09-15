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
		if ( !NPCPrefab.IsValid() )
			return;

		var randomPoint = Scene.NavMesh.GetRandomPoint().GetValueOrDefault();

		var clone = NPCPrefab.Clone();

		clone.Transform.Position = randomPoint;

		clone.NetworkSpawn( null );

		NextSpawn = 5;
	}
}
