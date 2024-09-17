using System.Threading.Tasks;
using Sandbox;
using Sandbox.Citizen;
using Sandbox.Events;
using Sandbox.Network;

public sealed class GameSystem : Component, Component.INetworkListener, IGameEventHandler<PlayerDeath>
{
	[Property] public GameObject PlayerPrefab { get; set; }

	public static bool PVP { get; set; } = false;

	[Property] public bool StartServer { get; set; } = true;

	[Property] public bool SpawnPlayer { get; set; } = true;

	protected override async Task OnLoad()
	{
		if ( Networking.IsHost && !GameNetworkSystem.IsActive && StartServer && !Scene.IsEditor )
		{
			LoadingScreen.Title = "Creating Lobby...";
			await Task.DelaySeconds( 0.1f );
			GameNetworkSystem.CreateLobby();
		}
	}

	public void OnActive( Connection connection )
	{
		connection.CanRefreshObjects = true;

		if ( !PlayerPrefab.IsValid() || !SpawnPlayer )
			return;

		var spawns = Scene.GetAllComponents<SpawnPoint>().ToList();

		Transform SpawnTransform = new();

		if ( spawns.Count() > 0 )
		{
			SpawnTransform = Game.Random.FromList( spawns ).Transform.World;
		}
		else
		{
			SpawnTransform = Transform.World;
		}

		var player = PlayerPrefab.Clone( SpawnTransform );

		if ( player.Components.TryGet<CitizenAnimationHelper>( out var animHelper, FindMode.EnabledInSelfAndChildren ) && animHelper.Target.IsValid() )
		{
			var clothing = new ClothingContainer();

			clothing.Deserialize( connection.GetUserData( "avatar" ) );
			clothing.Apply( animHelper.Target );
		}

		player.NetworkSpawn( connection );

		Scene.Dispatch( new OnPlayerJoin() );
	}

	void IGameEventHandler<PlayerDeath>.OnGameEvent( PlayerDeath eventArgs )
	{
		var player = eventArgs.Player;

		if ( !player.IsValid() )
			return;

		var spawns = Scene.GetAllComponents<SpawnPoint>()?.ToList();

		if ( spawns.Count() > 0 )
		{
			player.SetWorld( Game.Random.FromList( spawns ).Transform.World );
		}
		else
		{
			eventArgs.Player.Transform.World = Transform.World;
		}

		player.GameObject.Dispatch( new PlayerReset() );
	}
}
