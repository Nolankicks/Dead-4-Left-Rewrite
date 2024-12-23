public sealed class MapLoadingSystem : GameObjectSystem<MapLoadingSystem>, ISceneStartup
{
	public MapLoadingSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.SceneLoaded, 1, OnSceneLoad, "OnSceneLoad" );
	}

	void ISceneStartup.OnHostInitialize()
	{
		Log.Info( "Host Initialized" );

		//If we are a dedicated server, load a scene
		if ( Application.IsHeadless )
			Scene.LoadFromFile( "scenes/easter.scene" );
	}

	void OnSceneLoad()
	{
		//Don't load the engine scene if it's already loaded
		if ( Scene.GetAll<GameSystem>().Count() > 0 || Scene.IsEditor || Scene.GetAll<MainMenu>().Count() > 0 )
			return;

		var core = GameObject.Clone( ResourceLibrary.Get<PrefabFile>( "prefabs/core.prefab" ) );
		core.BreakFromPrefab();
	}
}
