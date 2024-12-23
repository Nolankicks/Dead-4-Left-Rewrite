using Sandbox;

public sealed class Destoryer : Component
{
	[Property] public float Time { get; set; } = 10;

	public TimeUntil DestroyTime { get; set; } = 0;

	protected override void OnStart()
	{
		if ( Networking.IsHost )
			DestroyTime = Time;
	}

	protected override void OnUpdate()
	{
		if ( DestroyTime && Networking.IsHost )
		{
			GameObject.Destroy();
		}
	}
}
