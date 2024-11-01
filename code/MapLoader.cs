public sealed class MapLoader : GameObjectSystem<MapLoader>, ISceneStartup
{
	public MapLoader( Scene scene ) : base( scene ) {}

	void ISceneStartup.OnHostInitialize()
	{
		var slo = new SceneLoadOptions();
		slo.IsAdditive = true;
		slo.SetScene( "scenes/dead4leftmainrewrite.scene" );
		Scene.Load( slo );
	}
}
