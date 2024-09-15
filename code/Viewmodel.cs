using Sandbox;
using Sandbox.Events;

public sealed class Viewmodel : Component, IGameEventHandler<JumpEvent>
{
	float lastPitch;
	float lastYaw;
	float yawInertia;
	float pitchInertia;
	bool UseInteria = false;

	[Property] public SkinnedModelRenderer Renderer { get; set; }

	protected override void OnStart()
	{
		if ( GameObject.Parent.IsProxy )
		{
			foreach ( var renderer in Components.GetAll<SkinnedModelRenderer>().ToList() )
			{
				renderer.Enabled = false;
			}

			Enabled = false;
		}
	}

	protected override void OnUpdate()
	{
		if ( GameObject.Parent.IsProxy )
			return;
		
		ApplyInertia();

		var local = PlayerController.Local;

		if ( !local.IsValid() || !Renderer.IsValid() && !local.shrimpleCharacterController.IsValid() )
			return;

		Renderer.Set( "b_grounded", local.shrimpleCharacterController.IsOnGround );
	}

	void ApplyInertia()
	{
		var camera = Scene.GetAllComponents<CameraComponent>().FirstOrDefault( x => !x.IsProxy );
		var controller = PlayerController.Local?.shrimpleCharacterController;

		if ( !camera.IsValid() || !Renderer.IsValid() || !controller.IsValid() )
			return;

		var cameraRot = camera.Transform.Rotation;

		if ( !UseInteria )
		{
			lastPitch = cameraRot.Pitch();
			lastYaw = cameraRot.Yaw();
			yawInertia = 0;
			pitchInertia = 0;
			UseInteria = true;
		}
		var pitch = cameraRot.Pitch();
		var yaw = cameraRot.Yaw();
		pitchInertia = Angles.NormalizeAngle( pitch - lastPitch );
		yawInertia = Angles.NormalizeAngle( yaw - lastYaw );
		lastPitch = pitch;
		lastYaw = yaw;
		
		Renderer.Set( "aim_yaw_inertia", yawInertia * 2 );
		Renderer.Set( "aim_pitch_inertia", pitchInertia * 2 );
		Renderer.Set( "move_bob", controller.Velocity.Length.Remap( 0, 300, 0, 1, true ) );
	}

	void IGameEventHandler<JumpEvent>.OnGameEvent( JumpEvent eventArgs )
	{
		if ( GameObject.Parent.IsProxy )
			return;

		if ( Renderer.IsValid() )
			Renderer.Set( "b_jump", true );
	}
}
