using System;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Sdf;
using Sandbox.Utility;

public sealed class WorldGen : Component
{
    [Property] public Sdf3DWorld World { get; set; }
    [Property] public Sdf3DVolume Volume { get; set; }
    [Property] public WorldParameters Parameters { get; set; }
    [Property] public string Seed { get; set; }

    protected override void OnStart()
    {
        if ( Networking.IsHost )
        {
            GenerateButton();
        }
    }

    [Button]
    public void GenerateButton()
    {
        var bytes = new byte[8];

		Random.Shared.NextBytes( bytes );

		Seed = string.Join( "", bytes.Select( x => x.ToString( "x2" ) ) );

        _ = Generate();
    }

    
    public async Task Generate()
    {
        if ( !World.IsValid() || Volume is null || Parameters is null || Parameters.Ground is null || Transform is null )
            return;

        await World.ClearAsync();

        var voxelRes = (int)(World.Size.x * Parameters.Ground.ChunkResolution / Parameters.Ground.ChunkSize);

        var heightmapNoise = Parameters.GetHeightmapField( Seed.FastHash(), Transform.World, 1 );
		var caveNoise = new CaveNoiseField(
			Noise.SimplexField( new Noise.FractalParameters( Octaves: 6, Frequency: 1f / 4096f ) ),
			Noise.SimplexField( new Noise.FractalParameters( Octaves: 2, Frequency: 1f / 16384f ) ) );
		var caveSdf = new NoiseSdf3D( caveNoise, 0.6f, 256f / World.Transform.Scale.x )
			.Transform( new Transform( -Transform.Position / World.Transform.Scale.x, Rotation.Identity,
				1f / World.Transform.Scale.x ) );
		var heightmapSdf = new HeightmapSdf3D( heightmapNoise, voxelRes, World.Size.x );
		var finalSdf = heightmapSdf.Intersection( caveSdf );

        await Task.MainThread();
        await World.AddAsync( finalSdf, Parameters.Ground );
    }
}

[GameResource( "World Parameters", "world", "Parameters for procedurally generating worlds.", Icon = "public" )]
public sealed class WorldParameters : GameResource
{
    [Property]
    public Sdf3DVolume Ground { get; set; }

    [Property]
    public float IslandNoiseScale { get; set; } = 65536f;

    [Property]
    public float TerrainNoiseScale { get; set; } = 16384f;

    [Property]
    public float HeightNoiseScale { get; set; } = 4096f;

    [Property]
    public Curve IslandBias { get; set; }

    [Property]
    public Curve TerrainBias { get; set; }

    [Property]
    public Curve PlainsCurve { get; set; }

    [Property]
    public Curve MountainsCurve { get; set; }

    public INoiseField GetHeightmapField( int seed, Transform transform, int level )
    {
        return new HeightmapField(
            Noise.SimplexField( new Noise.FractalParameters( seed, Frequency: 1f / IslandNoiseScale, Octaves: 8 ) ),
            Noise.SimplexField( new Noise.FractalParameters( seed, Frequency: 1f / TerrainNoiseScale, Octaves: 4 ) ),
            Noise.SimplexField( new Noise.FractalParameters( seed, Frequency: 1f / HeightNoiseScale, Octaves: 8 ) ),
            IslandBias,
            TerrainBias,
            PlainsCurve,
            MountainsCurve,
            transform,
            1f / (1 << level) );
    }

    private record HeightmapField(
        INoiseField IslandField,
        INoiseField TerrainField,
        INoiseField HeightField,
        Curve IslandBias,
        Curve TerrainBias,
        Curve PlainsCurve,
        Curve MountainsCurve,
        Transform Transform,
        float Scale ) : INoiseField
    {
        public float Sample( float x )
        {
            throw new NotImplementedException();
        }

        public float Sample( float x, float y )
        {
            var worldPos = (Vector2)Transform.PointToWorld( new Vector3( x, y, 0f ) / Scale );

            var island = IslandField.Sample( worldPos );
            var terrain = TerrainField.Sample( worldPos );
            var height = HeightField.Sample( worldPos );

            island = IslandBias.Evaluate( island );
            terrain = TerrainBias.Evaluate( terrain );

            var plainsHeight = PlainsCurve.Evaluate( height );
            var mountainsHeight = MountainsCurve.Evaluate( height );
            var oceanHeight = 512f + height * 128f;
            var landHeight = plainsHeight + terrain * (mountainsHeight - plainsHeight);

            height = oceanHeight + island * (landHeight - oceanHeight);

            return height * Scale - 2f;
        }

        public float Sample( float x, float y, float z )
        {
            throw new NotImplementedException();
        }
    }

}

file record CaveNoiseField( INoiseField BaseNoise, INoiseField ThresholdNoise ) : INoiseField
{
    public float Sample( float x )
    {
        throw new NotImplementedException();
    }

    public float Sample( float x, float y )
    {
        throw new NotImplementedException();
    }

    public float Sample( float x, float y, float z )
    {
        var threshold = ThresholdNoise.Sample( x, y );

        return BaseNoise.Sample( x, y, z * 2f ) * Math.Clamp( (z - 64f - threshold * 192f) / 256f, 0f, 1f );
    }

}
