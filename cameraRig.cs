using Godot;
using System;

public partial class cameraRig : Node3D
{
	
	public Node3D _yaw;
	public Node3D _pitch;
	public Camera3D _camera;

	private bool scroll_press = false;
	private bool shift_press = false;
	private Vector3 _rightVec, _forwardVec;

	private float _zoomSensitivity = 2.0f;
	private float _rotationSensitivity = 800f;
	private float DragSpeed = 0.01f;
	private float _rotationX = 0.0f;
	private float _rotationY = 0.0f;
	private float _screenRatio = 0.0f;
	private int inverseScroll = 0;

	public override void _Ready() 
	{
		Vector2 screenSize= GetViewport().GetVisibleRect().Size;
		_screenRatio = screenSize.Y / screenSize.X;
		GD.Print("hello from nvim");
		_yaw = GetNode<Node3D>("%Yaw");
		_pitch = GetNode<Node3D>("%Pitch");
		_camera = GetNode<Camera3D>("%Camera3D");
		_GetMoveVectors();
	}

	public void _GetMoveVectors(){
		Vector3 offset = _camera.GlobalPosition - GlobalPosition;
		GD.Print(_camera.GlobalPosition);
		_rightVec = _camera.Transform.Basis.X;
		_forwardVec = new Vector3(offset.X, 0, offset.Z).Normalized();
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseMotion) 
		{
			if (shift_press == true && scroll_press == true)
			{
				// Pan the camera using the 
				GlobalPosition += _rightVec * -mouseMotion.Relative.X *
					DragSpeed + _forwardVec * -mouseMotion.Relative.Y *
					DragSpeed / _screenRatio;
				GD.Print("GlobalPosition" + GlobalPosition);
			}
			else if (scroll_press == true)
			{
				// Rotation using the 3D rig
				_rotationY = mouseMotion.Relative.Y / _rotationSensitivity * Mathf.Pi;
				_rotationX = mouseMotion.Relative.X / _rotationSensitivity * Mathf.Pi;
				_yaw.RotateY(-_rotationX);
				_pitch.RotateX(-_rotationY);
			}
		}
		else if (@event is InputEventMouseButton mb) 
		{
			if (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown)
			{
				float zoomSize = _camera.Fov + _zoomSensitivity * (mb.ButtonIndex == MouseButton.WheelUp ? -1f : 1f);
				_camera.Fov = Mathf.Clamp(zoomSize, 0.20f, 1000f);

			}
			if (mb.ButtonIndex == MouseButton.Middle && mb.Pressed == true) 
			{
				//RotateObjectLocal(new Vector3(1,0,0), 0.1f);
				scroll_press = true;
			} else if (mb.ButtonIndex == MouseButton.Middle  && mb.Pressed == false) 
			{
				scroll_press = false;
			}
		}
		else if (@event is InputEventKey eventKey) {
			if (eventKey.Pressed == true && eventKey.Keycode == Key.Shift){
				shift_press = true;
				GD.Print("shift_press= " + shift_press);
			}
			else if (eventKey.Pressed == false && eventKey.Keycode == Key.Shift)
			{
				shift_press = false;
				GD.Print("shift_press= " + shift_press);
			}
		}
	}
}
