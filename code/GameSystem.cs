using System.Threading.Tasks;
using Sandbox;
using Sandbox.Citizen;
using Sandbox.Sdf;
using Sandbox.Events;
using Sandbox.Network;

public sealed class GameSystem : Component, Component.INetworkListener, IGameEventHandler<PlayerDeath>
{
	[Property] public GameObject PlayerPrefab { get; set; }
	[Property] public bool StartServer { get; set; } = true;
	[Property] public bool ShouldSpawnPlayer { get; set; } = true;

	public static bool PVP { get; set; } = false;

	protected override async Task OnLoad()
	{
		if ( Networking.IsHost && !GameNetworkSystem.IsActive && StartServer )
		{
			LoadingScreen.Title = "Creating Lobby...";
			await Task.DelaySeconds( 0.1f );
			GameNetworkSystem.CreateLobby();
		}
	}

	protected override void OnStart()
	{
		if ( !Networking.IsHost && !StartServer )
			return;
	
		SpawnPlayer( GetSpawnTransform() );
	}

	public void OnActive( Connection connection )
	{
		connection.CanRefreshObjects = true;

		SpawnPlayer( GetSpawnTransform(), connection );
	}
	public void SpawnPlayer( Transform SpawnTransform, Connection connection = null )
	{
		if ( !PlayerPrefab.IsValid() || !ShouldSpawnPlayer )
			return;

		var player = PlayerPrefab.Clone( SpawnTransform );

		if ( player.Components.TryGet<CitizenAnimationHelper>( out var animHelper, FindMode.EnabledInSelfAndChildren ) && animHelper.Target.IsValid() )
		{
			var clothing = new ClothingContainer();

			var clothingConnection = connection ?? Connection.Local;

			clothing.Deserialize( clothingConnection.GetUserData( "avatar" ) );
			
			clothing.Apply( animHelper.Target );
		}

		if ( connection is not null )
			player.NetworkSpawn( connection );

		Scene.Dispatch( new OnPlayerJoin() );
	}

	public Transform GetSpawnTransform()
	{
		var spawns = Scene.GetAllComponents<SpawnPoint>().ToList();

		if ( spawns.Count() > 0 )
		{
			return Game.Random.FromList( spawns ).Transform.World;
		}
		else
		{
			return Transform.World;
		}
	}

	void IGameEventHandler<PlayerDeath>.OnGameEvent( PlayerDeath eventArgs )
	{
		var player = eventArgs.Player;

		if ( !player.IsValid() )
			return;

		player.SetWorld( GetSpawnTransform() );

		player.GameObject.Dispatch( new PlayerReset() );
	}
}
